/*
 * Script.cs --
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
using Eagle._Containers.Public;

#if TEST
using _Helpers = Eagle._Tests.Default.Helpers;
#endif

namespace Zeus.Providers
{
    /// <summary>
    /// Implements an RFC 2898 (PBKDF2) data provider that obtains its
    /// key-derivation parameters by evaluating an Eagle script.  The script
    /// text is carried in the caller data; when <see cref="GetData" /> runs,
    /// the script is evaluated (with this provider exposed to it) so it can
    /// populate the parameters, after which the base provider supplies any
    /// that remain.  This provider is only functional in test builds.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("d8ab280a-f0e4-4f34-a50e-33ab242d644c")]
    public sealed class Script : Core
    {
        #region Internal Constructors
        /// <summary>
        /// Constructs a new <see cref="Script" /> provider instance
        /// associated with the specified interpreter and caller data.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter this provider is associated with.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller; expected to carry the
        /// script text to evaluate.
        /// </param>
        internal Script(
            Interpreter interpreter, /* in */
            IClientData clientData   /* in */
            )
            : base(interpreter, clientData)
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Attempts to extract the script text from the caller data attached
        /// to this provider.
        /// </summary>
        /// <param name="text">
        /// Upon success, receives the script text to evaluate.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero if the script text was successfully extracted; otherwise,
        /// zero.
        /// </returns>
        private bool TryUnpackClientData(
            ref string text, /* out */
            ref Result error /* out */
            )
        {
            IClientData clientData = base.ClientData;

            if (clientData == null)
            {
                error = "invalid clientData";
                return false;
            }

            string localText = clientData.Data as string;

            if (localText == null)
            {
                error = "invalid script";
                return false;
            }

            text = localText;
            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IRfc2898DataProvider Members
        //
        // BUGBUG: The use of a plain string here instead of something like
        //         the SecureString class is due to the requirements of the
        //         Rfc2898DeriveBytes class.
        //
        /// <summary>
        /// Supplies the RFC 2898 key-derivation parameters by evaluating the
        /// script carried in this provider's caller data, then deferring to
        /// the base implementation to fill in any still-missing parameters.
        /// In non-test builds this method is not implemented and returns an
        /// error.
        /// </summary>
        /// <param name="fileName">
        /// An optional file name exposed to the script and passed through to
        /// the base provider.
        /// </param>
        /// <param name="encodingName">
        /// An optional encoding name exposed to the script and passed through
        /// to the base provider.
        /// </param>
        /// <param name="password">
        /// On input, the caller-supplied password, if any; on output, may
        /// receive a password produced by the script.
        /// </param>
        /// <param name="salt">
        /// On input, the caller-supplied salt, if any; on output, may receive
        /// a salt produced by the script.
        /// </param>
        /// <param name="iterationCount">
        /// On input, the caller-supplied iteration count, if any; on output,
        /// may receive an iteration count produced by the script.
        /// </param>
        /// <param name="hashAlgorithmName">
        /// On input, the caller-supplied hash algorithm name, if any; on
        /// output, may receive a hash algorithm name produced by the script.
        /// </param>
        /// <param name="signature">
        /// On input, the caller-supplied signature, if any; on output, may
        /// receive a signature produced by the script.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public override ReturnCode GetData(
            string fileName,              /* in: OPTIONAL */
            string encodingName,          /* in: OPTIONAL */
            ref string password,          /* in, out */
            ref string salt,              /* in, out */
            ref int iterationCount,       /* in, out */
            ref string hashAlgorithmName, /* in, out */
            ref string signature,         /* in, out */
            ref Result error              /* out */
            )
        {
#if TEST
            Result localResult; /* REUSED */
            string text = null;

            localResult = null;

            if (!TryUnpackClientData(ref text, ref localResult))
            {
                error = localResult;
                return ReturnCode.Error;
            }

            ObjectDictionary objects = new ObjectDictionary();

            objects.Add("provider", this);
            objects.Add("fileName", fileName);
            objects.Add("encodingName", encodingName);

            localResult = null;

            if (_Helpers.EvaluateScript(base.Interpreter,
                    text, objects, ObjectFlags.None,
                    Utility.GetObjectDefaultSynchronous(),
                    Utility.GetObjectDefaultDispose(),
                    ref localResult) != ReturnCode.Ok)
            {
                error = localResult;
                return ReturnCode.Error;
            }

            return base.GetData(
                fileName, encodingName, ref password, ref salt,
                ref iterationCount, ref hashAlgorithmName,
                ref signature, ref error);
#else
            error = "not implemented";
            return ReturnCode.Error;
#endif
        }
        #endregion
    }
}
