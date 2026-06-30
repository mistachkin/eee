/*
 * Obfuscated.cs --
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
using Zeus.Components.Private;

namespace Zeus.Procedures
{
    /// <summary>
    /// Implements an "obfuscated" script procedure, an extension of
    /// <see cref="Registered" /> whose body is encrypted at rest.  At
    /// construction the plaintext body is encrypted (using the plugin's RFC
    /// 2898 parameters and Rijndael) and the inspectable fields are cleared;
    /// during execution the body is decrypted just-in-time, the base
    /// (verifying) execution runs, and a finally block restores the encrypted
    /// body.
    /// </summary>
    [ObjectId("fd6e5169-f91e-4bce-b596-84548d036a8b")]
    internal sealed class Obfuscated : Registered
    {
        #region Private Data
        //
        // NOTE: This is the parent plugin associated with this procedure.
        //       For now, this will always be the Zeus plugin; however, it
        //       may be the case in the future that this will point to a
        //       different plugin.
        //
        /// <summary>
        /// The plugin whose RFC 2898 parameters are used to encrypt and
        /// decrypt this procedure's body.
        /// </summary>
        private IPlugin plugin;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="Obfuscated" /> procedure from the
        /// supplied procedure data, clearing the inspectable fields and
        /// marking the procedure as obfuscated.
        /// </summary>
        /// <param name="procedureData">
        /// The data used to create and configure the procedure.
        /// </param>
        private Obfuscated(
            IProcedureData procedureData /* in */
            )
            : base(procedureData, null)
        {
            /* NO RESULT */
            ClearData();

            /* NO RESULT */
            MarkAsObfuscated();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new <see cref="Obfuscated" /> procedure for the
        /// specified plugin, encrypting the body and creating the inner core
        /// procedure.
        /// </summary>
        /// <param name="procedureData">
        /// The data used to create and configure the procedure; its body is
        /// encrypted in place.
        /// </param>
        /// <param name="plugin">
        /// The plugin whose RFC 2898 parameters are used for encryption.
        /// </param>
        public Obfuscated(
            IProcedureData procedureData, /* in */
            IPlugin plugin                /* in */
            )
            : this(procedureData)
        {
            /* NO RESULT */
            SetPlugin(plugin);

            /* NO RESULT */
            SetupProcedureOrThrow(procedureData); /* throw */
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Internal Methods
        /// <summary>
        /// Encrypts or decrypts the supplied body using the RFC 2898
        /// parameters obtained from the associated plugin and the Rijndael
        /// symmetric algorithm.
        /// </summary>
        /// <param name="encrypt">
        /// Non-zero to encrypt the body; zero to decrypt it.
        /// </param>
        /// <param name="body">
        /// On input, the body to transform; on output, the transformed body.
        /// </param>
        /// <exception cref="ScriptException">
        /// Thrown when the key-derivation parameters cannot be obtained or the
        /// transform fails.
        /// </exception>
        internal void Transform(
            bool encrypt,   /* in */
            ref string body /* in, out */
            )
        {
            string password = null;
            string salt = null;
            int iterationCount = 0;
            string hashAlgorithmName = null;
            string signature = null; /* NOT USED */
            Result error; /* REUSED */

            error = null;

            if (Rfc2898Ops.GetData(
                    plugin, null, null, ref password,
                    ref salt, ref iterationCount,
                    ref hashAlgorithmName, ref signature,
                    ref error) != ReturnCode.Ok)
            {
                throw new ScriptException(error);
            }

            error = null;

            if (CryptographyOps.Transform(
                    null, password, salt, iterationCount,
                    hashAlgorithmName, encrypt, ref body,
                    ref error) != ReturnCode.Ok)
            {
                throw new ScriptException(error);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Marks this procedure with the obfuscated procedure flag.
        /// </summary>
        private void MarkAsObfuscated()
        {
            this.Flags |= ProcedureFlags.Obfuscated;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the inspectable procedure fields (arguments, named
        /// arguments, overwrite arguments, and body) to prevent the
        /// plaintext from being examined.
        /// </summary>
        private void ClearData()
        {
            this.Arguments = null;
            this.NamedArguments = null;
            this.OverwriteArguments = null;
            this.Body = null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the plugin whose RFC 2898 parameters are used for encryption
        /// and decryption.
        /// </summary>
        /// <param name="plugin">
        /// The plugin to associate with this procedure.
        /// </param>
        private void SetPlugin(
            IPlugin plugin /* in */
            )
        {
            this.plugin = plugin;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Encrypts the procedure body, marks the procedure data as
        /// obfuscated, creates the inner core procedure from it, and records
        /// that procedure for execution.
        /// </summary>
        /// <param name="procedureData">
        /// The procedure data whose body is encrypted and from which the inner
        /// procedure is created.
        /// </param>
        /// <exception cref="ScriptException">
        /// Thrown when the body cannot be encrypted or the inner procedure
        /// cannot be created.
        /// </exception>
        private void SetupProcedureOrThrow(
            IProcedureData procedureData /* in */
            )
        {
            string body = procedureData.Body; /* decrypted */

            /* NO RESULT */
            Transform(true, ref body);

            procedureData.Body = body; /* encrypted */
            procedureData.Flags |= ProcedureFlags.Obfuscated;

            IProcedure procedure = null;
            Result error = null;

            procedure = Utility.NewCoreProcedure(
                procedureData, ref error);

            if (procedure == null)
                throw new ScriptException(error);

            /* IGNORED */
            MaybeSetProcedure(procedure);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes the obfuscated procedure.  The inner core procedure (which
        /// must be marked obfuscated) has its body decrypted just-in-time, the
        /// base (verifying) execution runs, and a finally block always
        /// restores the encrypted body afterward.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the procedure is being executed.
        /// </param>
        /// <param name="clientData">
        /// The extra data supplied by the caller, if any.
        /// </param>
        /// <param name="arguments">
        /// The list of arguments supplied to the procedure.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of the procedure, or an error
        /// message describing why it failed.
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
            ref Result result        /* out */
            )
        {
            IProcedure procedure = GetProcedure();

            if (procedure == null)
            {
                result = String.Format(
                    "invalid core procedure for {0}",
                    Utility.FormatWrapOrNull(this.Name));

                return ReturnCode.Error;
            }

            if (!Utility.HasFlags(procedure.Flags,
                    ProcedureFlags.Obfuscated, true))
            {
                result = String.Format(
                    "core procedure {0} not obfuscated",
                    Utility.FormatWrapOrNull(procedure.Name));

                return ReturnCode.Error;
            }

            string savedBody = procedure.Body;

            try
            {
                string body = savedBody;

                Transform(false, ref body);

                procedure.Body = body;

                return base.Execute(
                    interpreter, clientData, arguments,
                    ref result);
            }
            catch (Exception e)
            {
                //
                // TODO: Mask the error message here to avoid
                //       disclosing unnecessary information?
                //
                result = e;
                return ReturnCode.Error;
            }
            finally
            {
                procedure.Body = savedBody;
            }
        }
        #endregion
    }
}
