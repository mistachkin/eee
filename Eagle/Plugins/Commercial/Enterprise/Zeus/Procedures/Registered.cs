/*
 * Registered.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Zeus.Components.Private;

namespace Zeus.Procedures
{
    /// <summary>
    /// Implements a tamper-evident "registered" script procedure.  A
    /// registered procedure's name is the SHA-512 hash of its body, saved
    /// arguments, and procedure flags; before each execution the hash is
    /// recomputed and verified against the name, so any modification causes
    /// execution to be refused.  The name cannot be changed, and the
    /// read-only flag prevents removal by ordinary code.  This is the base
    /// class for the <see cref="Obfuscated" /> procedure.
    /// </summary>
    [ObjectId("cd521d0f-cef0-4e4c-916e-9560b43e8817")]
    internal class Registered : Eagle._Procedures.Default
    {
        #region Public Constants
        //
        // HACK: This is the hash algorithm to use when creating
        //       and/or updating "registered" script procedures.
        //
        /// <summary>
        /// The name of the hash algorithm used to compute and verify
        /// registered procedure names.
        /// </summary>
        public static readonly string HashAlgorithmName = "SHA512";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This will be the actual procedure that should be
        //       used to service our IExecute.Execute method.
        //
        /// <summary>
        /// The inner procedure that actually services this procedure's
        /// execution, or null to fall back to the base implementation.
        /// </summary>
        private IProcedure procedure = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="Registered" /> procedure from the
        /// supplied procedure data.
        /// </summary>
        /// <param name="procedureData">
        /// The data used to create and configure the procedure.
        /// </param>
        private Registered(
            IProcedureData procedureData /* in */
            )
            : base(procedureData)
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Constructors
        /// <summary>
        /// Constructs a new <see cref="Registered" /> procedure from the
        /// supplied procedure data, overriding the body when a non-null body
        /// is supplied.
        /// </summary>
        /// <param name="procedureData">
        /// The data used to create and configure the procedure.
        /// </param>
        /// <param name="body">
        /// The procedure body to use instead of the one in the procedure
        /// data, or null to keep the existing body.
        /// </param>
        protected Registered(
            IProcedureData procedureData, /* in */
            string body                   /* in */
            )
            : this(procedureData)
        {
            //
            // HACK: If the specified body is non-null, it will simply
            //       override the body present in the procedure data.
            //
            /* IGNORED */
            MaybeSetBody(body);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new <see cref="Registered" /> procedure that wraps an
        /// existing procedure.  When the supplied procedure is non-null,
        /// execution delegates to it (after verification); the saved arguments
        /// and procedure flags are recorded for use during verification.
        /// </summary>
        /// <param name="procedure">
        /// The inner procedure to wrap and delegate to, or null.
        /// </param>
        /// <param name="savedArguments">
        /// The saved arguments to record for verification, or null.
        /// </param>
        /// <param name="savedProcedureFlags">
        /// The saved procedure flags to record for verification, or null.
        /// </param>
        public Registered(
            IProcedure procedure,               /* in */
            string savedArguments,              /* in */
            ProcedureFlags? savedProcedureFlags /* in */
            )
            : this(procedure as IProcedureData)
        {
            //
            // HACK: If the specified procedure is non-null, it will
            //       cause our IExecute.Execute method implementation
            //       to simply delegate to it; otherwise, it will use
            //       our base class (i.e. which does nothing).
            //
            /* IGNORED */
            MaybeSetProcedure(procedure);

            /* IGNORED */
            MaybeSetSavedArguments(savedArguments);

            /* IGNORED */
            MaybeSetSavedProcedureFlags(savedProcedureFlags);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
        /// <summary>
        /// Gets the inner procedure that execution delegates to, if any.
        /// </summary>
        /// <returns>
        /// The inner procedure, or null when there is none.
        /// </returns>
        protected internal IProcedure GetProcedure()
        {
            return this.procedure;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the inner procedure when a non-null procedure is supplied.
        /// </summary>
        /// <param name="procedure">
        /// The inner procedure to set, or null to leave it unchanged.
        /// </param>
        /// <returns>
        /// Non-zero when the procedure was set; otherwise, zero.
        /// </returns>
        protected bool MaybeSetProcedure(
            IProcedure procedure /* in */
            )
        {
            if (procedure == null)
                return false;

            this.procedure = procedure;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records the saved arguments when a non-null value is supplied.
        /// These participate in the registration name hash.
        /// </summary>
        /// <param name="savedArguments">
        /// The saved arguments to record, or null to leave them unchanged.
        /// </param>
        /// <returns>
        /// Non-zero when the saved arguments were set; otherwise, zero.
        /// </returns>
        public bool MaybeSetSavedArguments(
            string savedArguments /* in */
            )
        {
            if (savedArguments == null)
                return false;

            this.savedArguments = savedArguments;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records the saved procedure flags when a non-null value is
        /// supplied.  These participate in the registration name hash.
        /// </summary>
        /// <param name="savedProcedureFlags">
        /// The saved procedure flags to record, or null to leave them
        /// unchanged.
        /// </param>
        /// <returns>
        /// Non-zero when the saved procedure flags were set; otherwise, zero.
        /// </returns>
        public bool MaybeSetSavedProcedureFlags(
            ProcedureFlags? savedProcedureFlags /* in */
            )
        {
            if (savedProcedureFlags == null)
                return false;

            this.savedProcedureFlags = savedProcedureFlags;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the canonical hash input (flags, saved arguments, and body)
        /// used to compute or verify this procedure's registration name.
        /// </summary>
        /// <param name="body">
        /// The procedure body to include in the hash input.
        /// </param>
        /// <returns>
        /// A string builder containing the hash input.
        /// </returns>
        protected StringBuilder GetHashBuilder(
            string body /* in */
            )
        {
            return CommonOps.GetHashBuilderForRegisteredProcedure(
                body, this.savedArguments, this.savedProcedureFlags);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Sets this procedure's body when a non-null body is supplied.
        /// </summary>
        /// <param name="body">
        /// The body to set, or null to leave it unchanged.
        /// </param>
        /// <returns>
        /// Non-zero when the body was set; otherwise, zero.
        /// </returns>
        private bool MaybeSetBody(
            string body /* in */
            )
        {
            if (body == null)
                return false;

            this.Body = body;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Recomputes the registration hash from the supplied body (together
        /// with the saved arguments and flags) and verifies that it matches
        /// the supplied name, confirming the procedure has not been modified.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to compute the hash.
        /// </param>
        /// <param name="name">
        /// The registered procedure name to verify against.
        /// </param>
        /// <param name="body">
        /// The current procedure body to hash.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero when the recomputed hash matches the name; otherwise,
        /// zero.
        /// </returns>
        private bool HashAndVerifyAgainstName(
            Interpreter interpreter, /* in */
            string name,             /* in */
            string body,             /* in */
            ref Result error         /* out */
            )
        {
            if (name == null)
            {
                error = "invalid registered procedure name";
                return false;
            }

            if (body == null)
            {
                error = "invalid registered procedure body";
                return false;
            }

            byte[] hashValue = Utility.HashString(
                interpreter, Registered.HashAlgorithmName,
                GetHashBuilder(body).ToString(), EncodingType.Script,
                ref error);

            if (hashValue == null)
                return false;

            string localName = Utility.ToHexadecimalString(hashValue);

            if (localName == null)
            {
                error = "could not format registered procedure name";
                return false;
            }

            if (!Utility.SystemStringEquals(
                    localName, Utility.TailOnly(name)))
            {
                error = String.Format(
                    "registered procedure {0} could not be verified",
                    Utility.FormatWrapOrNull(name));

                return false;
            }

            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The backing field for the <see cref="SavedArguments" /> property.
        /// </summary>
        private string savedArguments;

        /// <summary>
        /// Gets the saved arguments that participate in this procedure's
        /// registration name hash.
        /// </summary>
        public string SavedArguments
        {
            get { return savedArguments; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="SavedProcedureFlags" />
        /// property.
        /// </summary>
        private ProcedureFlags? savedProcedureFlags;

        /// <summary>
        /// Gets the saved procedure flags that participate in this procedure's
        /// registration name hash.
        /// </summary>
        public ProcedureFlags? SavedProcedureFlags
        {
            get { return savedProcedureFlags; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IIdentifierName Overrides
        /// <summary>
        /// Gets the procedure name.  Setting the name is not supported and
        /// throws <see cref="NotSupportedException" />, which prevents the
        /// registration name (a hash of the procedure) from being changed.
        /// </summary>
        public override string Name
        {
            get { return base.Name; }
            set { throw new NotSupportedException(); }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IExecute Members
        /// <summary>
        /// Executes the registered procedure.  When an inner procedure is
        /// present, its registration hash is verified (unless the registered
        /// flag is absent) before execution is delegated to it; verification
        /// failure refuses execution.  When there is no inner procedure, the
        /// base implementation is used.
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

            if (procedure != null)
            {
                if (!Utility.HasFlags(
                        this.Flags, ProcedureFlags.Registered, true) ||
                    HashAndVerifyAgainstName(
                        interpreter, procedure.Name, procedure.Body,
                        ref result))
                {
                    return procedure.Execute(
                        interpreter, clientData, arguments, ref result);
                }
                else
                {
                    return ReturnCode.Error;
                }
            }
            else
            {
                return base.Execute(
                    interpreter, clientData, arguments, ref result);
            }
        }
        #endregion
    }
}
