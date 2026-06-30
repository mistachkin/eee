/*
 * Default.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if OBFUSCATION
using System.Reflection;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;

namespace Zeus.Providers
{
    /// <summary>
    /// Provides the abstract base class for all RFC 2898 (PBKDF2) data
    /// providers.  It declares the interpreter and caller-data accessors and
    /// the key-derivation parameter accessor, leaving their implementation to
    /// derived classes.  When isolated interpreters or plugins are enabled,
    /// it derives from <c>ScriptMarshalByRefObject</c> so a provider can be
    /// marshaled across application domain boundaries.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("50f03e9f-3a4e-4320-b72d-fd53f20e3d66")]
    public abstract class Default :
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
        ScriptMarshalByRefObject,
#endif
        IHaveInterpreter,
        IHaveClientData,
        IRfc2898DataProvider
    {
        #region IGetClientData / ISetClientData Members
        //
        // NOTE: This class is abstract and does not provide an implementation
        //       of this property.
        //
        /// <summary>
        /// Gets or sets the extra data associated with this provider by the
        /// caller, if any.
        /// </summary>
        public abstract IClientData ClientData { get; set; }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IGetInterpreter / ISetInterpreter Members
        //
        // NOTE: This class is abstract and does not provide an implementation
        //       of this property.
        //
        /// <summary>
        /// Gets or sets the interpreter this provider is associated with.
        /// </summary>
        public abstract Interpreter Interpreter { get; set; }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IRfc2898DataProvider Members
        //
        // NOTE: This class is abstract and does not provide an implementation
        //       of this method.
        //
        // BUGBUG: The use of a plain string here instead of something like
        //         the SecureString class is due to the requirements of the
        //         Rfc2898DeriveBytes class.
        //
        /// <summary>
        /// Supplies the RFC 2898 key-derivation parameters, filling in any
        /// that are missing from the supplied reference arguments according
        /// to the concrete provider's policy.
        /// </summary>
        /// <param name="fileName">
        /// An optional file name a provider may use to locate its data.
        /// </param>
        /// <param name="encodingName">
        /// An optional encoding name a provider may use when reading its
        /// data.
        /// </param>
        /// <param name="password">
        /// On input, the caller-supplied password, if any; on output, may
        /// receive a password supplied by the provider.
        /// </param>
        /// <param name="salt">
        /// On input, the caller-supplied salt, if any; on output, may receive
        /// a salt supplied by the provider.
        /// </param>
        /// <param name="iterationCount">
        /// On input, the caller-supplied iteration count, if any; on output,
        /// may receive an iteration count supplied by the provider.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On input, the caller-supplied hash algorithm name, if any; on
        /// output, may receive a hash algorithm name supplied by the
        /// provider.
        /// </param>
        /// <param name="signature">
        /// On input, the caller-supplied signature, if any; on output, may
        /// receive a signature supplied by the provider.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public abstract ReturnCode GetData(
            string fileName,              /* in: OPTIONAL */
            string encodingName,          /* in: OPTIONAL */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
        );
        #endregion
    }
}
