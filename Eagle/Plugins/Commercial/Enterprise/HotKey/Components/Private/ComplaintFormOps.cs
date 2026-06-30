/*
 * ComplaintFormOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Manages the shared "complaint form" used to display complaints from the
    /// script engine.  It evaluates the optional setup and cleanup scripts and
    /// enforces that at most one complaint form exists per application domain.
    /// </summary>
    [ObjectId("5f868a3c-5102-4ff6-b82b-526e97874689")]
    internal static class ComplaintFormOps
    {
        #region Public Constants
        //
        // NOTE: This is the name of an optional script variable that can be
        //       used to prevent the plugin complaint form from being created
        //       by the plugin instance via the Initialize method.
        //
        /// <summary>
        /// The name of an optional script variable that, when present,
        /// prevents the plugin from creating the complaint form during
        /// initialization.
        /// </summary>
        public static readonly string NoVariableName =
            "::" + typeof(Enterprise).FullName + "_NoComplaintForm";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
        /// <summary>
        /// The file name of the optional complaint form setup script.
        /// </summary>
        private const string ScriptFileName = "complaintForm.eagle";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script evaluated to clean up the complaint form, dispatching to
        /// the namespace-qualified or global cleanup procedure.
        /// </summary>
        private const string CleanupScript =
            "if {[namespace enable]} then " +
            "{::ComplaintForm::cleanupComplaintForm} else " +
            "{::cleanupComplaintForm}";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: There can be only one "complaint form" per application
        //       domain.  This variable helps to enforce that restriction.
        //
        /// <summary>
        /// The reference count enforcing that at most one complaint form
        /// exists per application domain.
        /// </summary>
        private static int levels;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hot-Key Complaint Form Helper Methods
        /// <summary>
        /// Determines whether a complaint form currently exists.
        /// </summary>
        /// <returns>
        /// Non-zero when a complaint form exists; otherwise, zero.
        /// </returns>
        private static bool HaveComplaintForm()
        {
            return Interlocked.CompareExchange(ref levels, 0, 0) > 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Atomically increments the complaint-form reference count.
        /// </summary>
        /// <returns>
        /// The new reference count.
        /// </returns>
        private static int EnterLevel()
        {
            return Interlocked.Increment(ref levels);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Atomically decrements the complaint-form reference count.
        /// </summary>
        /// <returns>
        /// The new reference count.
        /// </returns>
        private static int ExitLevel()
        {
            return Interlocked.Decrement(ref levels);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the optional complaint form setup script, creating the
        /// form when this is the first caller in the application domain.  The
        /// script is skipped for safe interpreters or when it does not exist.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the setup script.
        /// </param>
        /// <param name="started">
        /// On output, set to non-zero when this call started the complaint
        /// form.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode EvaluateSetupScript(
            Interpreter interpreter, /* in */
            ref bool started,        /* out */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            //
            // NOTE: If the interpreter is "safe", it cannot be used to setup
            //       or cleanup the complaint form.  Just skip it.
            //
            if (interpreter.IsSafe())
                return ReturnCode.Ok;

            //
            // NOTE: This script is entirely optional; therefore, just skip
            //       evaluating it if not found.
            //
            string fileName = Path.Combine(
                ScriptOps.GetDirectory(), ScriptFileName);

            if (!File.Exists(fileName))
                return ReturnCode.Ok;

            //
            // NOTE: Make sure there is only one of these forms present in the
            //       application domain.
            //
            if (EnterLevel() == 1)
            {
                //
                // NOTE: Evaluate the complaint form setup script.  This should
                //       create a new window to be used for the sole purpose of
                //       displaying "complaints" from the script engine.
                //
                ReturnCode code = ReturnCode.Ok;

                try
                {
                    code = interpreter.EvaluateFile(fileName, ref result);
                }
                catch (Exception e)
                {
                    result = e;
                    code = ReturnCode.Error;
                }
                finally
                {
                    if (code == ReturnCode.Ok)
                        started = true;
                }

                return code;
            }
            else
            {
                //
                // NOTE: We did not actually create the form; therefore, just
                //       undo the increment we performed above.
                //
                /* IGNORED */
                ExitLevel();
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the complaint form cleanup script, removing the form and
        /// its global state.  The script is skipped for safe interpreters,
        /// when this instance did not start the form, or when no form exists.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to evaluate the cleanup script.
        /// </param>
        /// <param name="started">
        /// On input, whether this instance started the form; set to zero when
        /// cleanup succeeds.
        /// </param>
        /// <param name="result">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode EvaluateCleanupScript(
            Interpreter interpreter, /* in */
            ref bool started,        /* in, out */
            ref Result result        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            //
            // NOTE: If the interpreter is "safe", it cannot be used to setup
            //       or cleanup the complaint form.  Just skip it.
            //
            if (interpreter.IsSafe())
                return ReturnCode.Ok;

            //
            // NOTE: Make sure that this instance is the one that started the
            //       complaint form.
            //
            if (!started)
                return ReturnCode.Ok;

            //
            // NOTE: Make sure there is at least one of these forms present in
            //       the application domain.
            //
            if (HaveComplaintForm())
            {
                //
                // NOTE: Evaluate the complaint form cleanup script.  This
                //       should get rid of the window and cleanup its global
                //       state.
                //
                ReturnCode code = ReturnCode.Ok;

                try
                {
                    code = interpreter.EvaluateScript(
                        CleanupScript, ref result);
                }
                catch (Exception e)
                {
                    result = e;
                    code = ReturnCode.Error;
                }
                finally
                {
                    if (code == ReturnCode.Ok)
                        started = false;
                }

                return code;
            }

            return ReturnCode.Ok;
        }
        #endregion
    }
}
