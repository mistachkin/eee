/*
 * CertificateShellOps.cs --
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
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Components.Public.Delegates;
using Eagle._Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides shell-oriented helper operations for evaluating scripts and
    /// files under certificate-based trust policies, including installation
    /// of the interpreter evaluation callbacks used by the licensing
    /// subsystem.
    /// </summary>
    [ObjectId("a5656708-3322-4a1e-8cf6-ec5ecdf381dd")]
    internal static class CertificateShellOps
    {
        #region Private Data
        /// <summary>
        /// The number of nested trusted (i.e. policy-checked) script or file
        /// evaluations currently in progress.
        /// </summary>
        private static int trustedLevels;
        /// <summary>
        /// The number of nested fallback (i.e. non-trusted) script or file
        /// evaluations currently in progress.
        /// </summary>
        private static int fallbackLevels;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Interface Methods
        /// <summary>
        /// Gets the current number of nested trusted script or file
        /// evaluations in progress.
        /// </summary>
        /// <returns>
        /// The current count of trusted evaluation levels.
        /// </returns>
        public static int GetTrustedLevels()
        {
            return Interlocked.CompareExchange(ref trustedLevels, 0, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current number of nested fallback script or file
        /// evaluations in progress.
        /// </summary>
        /// <returns>
        /// The current count of fallback evaluation levels.
        /// </returns>
        public static int GetFallbackLevels()
        {
            return Interlocked.CompareExchange(ref fallbackLevels, 0, 0);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified interpreter currently has any of
        /// the script or file evaluation callbacks installed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to check for installed evaluation callbacks.
        /// </param>
        /// <returns>
        /// Non-zero if at least one of the script, file, or encoded file
        /// evaluation callbacks is installed; otherwise, zero.
        /// </returns>
        public static bool HaveCallbacks(
            Interpreter interpreter /* in */
            )
        {
            if (interpreter != null)
            {
                if (interpreter.EvaluateScriptCallback != null)
                    return true;

                if (interpreter.EvaluateFileCallback != null)
                    return true;

                if (interpreter.EvaluateEncodedFileCallback != null)
                    return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Installs or removes the script and file evaluation callbacks on
        /// the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter on which the evaluation callbacks should be
        /// installed or removed.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the interpreter, used to determine
        /// whether cross-application-domain bridging is required.
        /// </param>
        /// <param name="install">
        /// Non-zero to install the evaluation callbacks; zero to remove them.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode InstallCallbacks(
            Interpreter interpreter, /* in */
            IPluginData pluginData,  /* in */
            bool install,            /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            EvaluateScriptCallback scriptCallback;
            EvaluateFileCallback fileCallback;
            EvaluateEncodedFileCallback encodedFileCallback;

            if (!GetCallbacks(
                    interpreter, pluginData, install,
                    out scriptCallback, out fileCallback,
                    out encodedFileCallback, ref error))
            {
                return ReturnCode.Error;
            }

            bool locked = false;

            try
            {
                interpreter.TryLockWithWait(
                    ref locked); /* TRANSACTIONAL */

                if (locked)
                {
                    interpreter.EvaluateScriptCallback = scriptCallback;
                    interpreter.EvaluateFileCallback = fileCallback;

                    interpreter.EvaluateEncodedFileCallback =
                        encodedFileCallback;

                    return ReturnCode.Ok;
                }
                else
                {
                    error = "interpreter is locked";
                }
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                interpreter.ExitLock(
                    ref locked); /* TRANSACTIONAL */
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Helper Methods
        /// <summary>
        /// Determines whether the policy of the specified type for the given
        /// interpreter is configured to trust only signed scripts or files.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose execution policy should be examined.
        /// </param>
        /// <param name="policyType">
        /// The type of policy to examine.
        /// </param>
        /// <param name="skipExists">
        /// Non-zero to also require the
        /// <see cref="ExecutionPolicy.SkipExists" /> flag as part of the
        /// policy comparison.
        /// </param>
        /// <returns>
        /// Non-zero if the policy is set to trust signed only; otherwise,
        /// zero.
        /// </returns>
        private static bool IsTrustSignedOnly(
            Interpreter interpreter, /* in */
            PolicyType policyType,   /* in */
            bool skipExists          /* in */
            )
        {
            ExecutionPolicy policy = ExecutionPolicy.TrustSignedOnly;

            if (skipExists)
                policy |= ExecutionPolicy.SkipExists;

            return Utility.HasFlags(
                CertificatePolicyOps.GetPolicy(interpreter, policyType),
                policy, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates the script, file, and encoded file evaluation callbacks to
        /// be installed on the specified interpreter, bridging across
        /// application domains when the interpreter and plugin are isolated.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which the evaluation callbacks are being
        /// created.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data associated with the interpreter, used to determine
        /// whether cross-application-domain bridging is required.
        /// </param>
        /// <param name="install">
        /// Non-zero to create the evaluation callbacks; zero to leave them
        /// null.
        /// </param>
        /// <param name="scriptCallback">
        /// Upon success, receives the script evaluation callback, or null
        /// when the callbacks are not being installed.
        /// </param>
        /// <param name="fileCallback">
        /// Upon success, receives the file evaluation callback, or null when
        /// the callbacks are not being installed.
        /// </param>
        /// <param name="encodedFileCallback">
        /// Upon success, receives the encoded file evaluation callback, or
        /// null when the callbacks are not being installed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool GetCallbacks(
            Interpreter interpreter,                             /* in */
            IPluginData pluginData,                              /* in */
            bool install,                                        /* in */
            out EvaluateScriptCallback scriptCallback,           /* out */
            out EvaluateFileCallback fileCallback,               /* out */
            out EvaluateEncodedFileCallback encodedFileCallback, /* out */
            ref Result error                                     /* out */
            )
        {
            scriptCallback = null;
            fileCallback = null;
            encodedFileCallback = null;

            if (install)
            {
                if (CertificateSharedOps.IsCrossAppDomain(
                        interpreter, pluginData))
                {
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                    ShellCallbackBridge callbackBridge =
                        ShellCallbackBridge.Create(
                            new CertificateShellCallback(), ref error);

                    if (callbackBridge == null)
                        return false;

                    scriptCallback = new EvaluateScriptCallback(
                        callbackBridge.EvaluateScriptCallback);

                    fileCallback = new EvaluateFileCallback(
                        callbackBridge.EvaluateFileCallback);

                    encodedFileCallback = new EvaluateEncodedFileCallback(
                        callbackBridge.EvaluateEncodedFileCallback);
#else
                    error = "cannot set delegates with plugin isolated";
                    return false;
#endif
                }
                else
                {
                    scriptCallback = new EvaluateScriptCallback(
                        EvaluateScriptCallback);

                    fileCallback = new EvaluateFileCallback(
                        EvaluateFileCallback);

                    encodedFileCallback = new EvaluateEncodedFileCallback(
                        EvaluateEncodedFileCallback);
                }
            }

            return true;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Script / File Evaluation Callbacks
        /// <summary>
        /// Evaluates the specified script text on behalf of the interpreter,
        /// applying the current certificate shell flags.  Implements the
        /// EvaluateScriptCallback delegate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script text is to be evaluated.
        /// </param>
        /// <param name="text">
        /// The script text to evaluate.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script text, or
        /// information about any error.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        /* Eagle._Components.Public.Delegates.EvaluateScriptCallback */
        public static ReturnCode EvaluateScriptCallback(
            Interpreter interpreter, /* in */
            string text,             /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            return EvaluateScript(
                interpreter, text, CertificateShellState.GetFlags(),
                ref result, ref errorLine);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the script contained in the specified file on behalf of
        /// the interpreter, applying the current certificate shell flags.
        /// Implements the EvaluateFileCallback delegate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script file is to be evaluated.
        /// </param>
        /// <param name="fileName">
        /// The name of the file containing the script to evaluate.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script file, or
        /// information about any error.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        /* Eagle._Components.Public.Delegates.EvaluateFileCallback */
        public static ReturnCode EvaluateFileCallback(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            return EvaluateEncodedFileCallback(
                interpreter, null, fileName, ref result, ref errorLine);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the script contained in the specified file, using the
        /// specified character encoding, on behalf of the interpreter and
        /// applying the current certificate shell flags.  Implements the
        /// EvaluateEncodedFileCallback delegate.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script file is to be evaluated.
        /// </param>
        /// <param name="encoding">
        /// The character encoding used to read the script file, or null to
        /// use the default encoding.
        /// </param>
        /// <param name="fileName">
        /// The name of the file containing the script to evaluate.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script file, or
        /// information about any error.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        /* Eagle._Components.Public.Delegates.EvaluateEncodedFileCallback */
        public static ReturnCode EvaluateEncodedFileCallback(
            Interpreter interpreter, /* in */
            Encoding encoding,       /* in */
            string fileName,         /* in */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            return EvaluateEncodedFile(
                interpreter, encoding, fileName,
                CertificateSharedOps.GetTimeout(interpreter, null),
                CertificateShellState.GetFlags(),
                ref result, ref errorLine);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Script / File Evaluation Implementation
        /// <summary>
        /// Evaluates the specified script text under the certificate trust
        /// policy, optionally falling back to a normal (i.e. non-trusted)
        /// evaluation depending on the supplied shell flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script text is to be evaluated.
        /// </param>
        /// <param name="text">
        /// The script text to evaluate.
        /// </param>
        /// <param name="flags">
        /// The optional shell flags controlling when fallback to a normal
        /// evaluation is permitted.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script text, or
        /// information about any error.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode EvaluateScript(
            Interpreter interpreter, /* in */
            string text,             /* in */
            ShellFlags? flags,       /* in: OPTIONAL */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (!interpreter.IsSafe())
            {
                if (CertificateSharedOps.HasFlags(
                        flags, ShellFlags.FallbackOnUnsafe, true))
                {
                    goto fallback;
                }
                else
                {
                    result = "interpreter is not safe";
                    return ReturnCode.Error;
                }
            }
            else if (!IsTrustSignedOnly(interpreter, PolicyType.File, true))
            {
                if (CertificateSharedOps.HasFlags(
                        flags, ShellFlags.FallbackOnNoPolicy, true))
                {
                    goto fallback;
                }
                else
                {
                    result = "script policy is not set to trust signed only";
                    return ReturnCode.Error;
                }
            }

            /* IGNORED */
            Interlocked.Increment(ref trustedLevels);

            try
            {
                IPolicyContext policyContext = null;
                Result localResult = null;

                if (CertificateSharedOps.CheckScriptAgainstPolicy(
                        interpreter, text, ref policyContext,
                        ref localResult) == ReturnCode.Ok)
                {
                    if (policyContext.IsApproved())
                    {
                        return interpreter.EvaluateTrustedScript(
                            text, Constants.ScriptTrustFlags,
                            ref result, ref errorLine);
                    }
                    else if (policyContext.IsDenied())
                    {
                        if (CertificateSharedOps.HasFlags(
                                flags, ShellFlags.FallbackOnDenied, true))
                        {
                            goto fallback;
                        }
                        else
                        {
                            if (localResult != null)
                                result = localResult;
                            else
                                result = "script denied by policy";
                        }
                    }
                    else
                    {
                        //
                        // NOTE: At this point, the policy context is either
                        //       officially "undecided" (one or more neutral
                        //       votes) or "none" (no votes whatsoever).  In
                        //       either of these cases, just fallback to the
                        //       normal (i.e. non-trusted) script evaluation.
                        //
                        if (CertificateSharedOps.HasFlags(
                                flags, ShellFlags.FallbackOnNeutral, true))
                        {
                            goto fallback;
                        }
                        else
                        {
                            result = "script not approved by policy";
                            return ReturnCode.Error;
                        }
                    }
                }
                else if (localResult != null)
                {
                    if (CertificateSharedOps.HasFlags(
                            flags, ShellFlags.FallbackOnFailure, true))
                    {
                        goto fallback;
                    }
                    else
                    {
                        result = localResult;
                    }
                }
                else
                {
                    if (CertificateSharedOps.HasFlags(
                            flags, ShellFlags.FallbackOnFailure, true))
                    {
                        goto fallback;
                    }
                    else
                    {
                        result = "script policy callback failed";
                    }
                }
            }
            catch (Exception e)
            {
                //
                // NOTE: Cannot fallback here because the script text
                //       MAY have been partially evaluated.
                //
                result = e;
            }
            finally
            {
                /* IGNORED */
                Interlocked.Decrement(ref trustedLevels);
            }

            return ReturnCode.Error;

        fallback:

            //
            // NOTE: Fallback to the normal (i.e. non-trusted) script
            //       text evaluation.  By default, this is used only
            //       when an interpreter is "unsafe" and/or its file
            //       policy is not configured; however, it can now be
            //       used when some kinds of failure are encountered.
            //
            EngineFlags engineFlags = EngineFlags.None;

            if (CertificateSharedOps.HasFlags(
                    flags, ShellFlags.NoPoliciesOnFallback, true))
            {
                engineFlags |= Constants.FallbackEngineFlags;
            }

            /* IGNORED */
            Interlocked.Increment(ref fallbackLevels);

            try
            {
                return interpreter.EvaluateScript(
                    text, engineFlags, ref result, ref errorLine);
            }
            finally
            {
                /* IGNORED */
                Interlocked.Decrement(ref fallbackLevels);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the script contained in the specified file under the
        /// certificate trust policy, optionally falling back to a normal,
        /// non-trusted evaluation depending on the supplied shell flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script file is to be evaluated.
        /// </param>
        /// <param name="fileName">
        /// The name of the file containing the script to evaluate.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout to apply when checking the file against the
        /// active policy.
        /// </param>
        /// <param name="flags">
        /// The optional shell flags controlling when fallback to a normal
        /// evaluation is permitted.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script file, or
        /// information about any error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode EvaluateFile(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            int? timeout,            /* in: OPTIONAL */
            ShellFlags? flags,       /* in: OPTIONAL */
            ref Result result        /* out */
            )
        {
            int errorLine = 0;

            return EvaluateEncodedFile(
                interpreter, null, fileName, timeout,
                flags, ref result, ref errorLine);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the script contained in the specified file under the
        /// certificate trust policy, optionally falling back to a normal,
        /// non-trusted evaluation depending on the supplied shell flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script file is to be evaluated.
        /// </param>
        /// <param name="fileName">
        /// The name of the file containing the script to evaluate.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout to apply when checking the file against the
        /// active policy.
        /// </param>
        /// <param name="flags">
        /// The optional shell flags controlling when fallback to a normal
        /// evaluation is permitted.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script file, or
        /// information about any error.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode EvaluateFile(
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            int? timeout,            /* in: OPTIONAL */
            ShellFlags? flags,       /* in: OPTIONAL */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            return EvaluateEncodedFile(
                interpreter, null, fileName, timeout,
                flags, ref result, ref errorLine);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the script contained in the specified file, using the
        /// specified character encoding, under the certificate trust policy,
        /// optionally falling back to a normal (i.e. non-trusted) evaluation
        /// depending on the supplied shell flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script file is to be evaluated.
        /// </param>
        /// <param name="encoding">
        /// The character encoding used to read the script file, or null to
        /// use the default encoding.
        /// </param>
        /// <param name="fileName">
        /// The name of the file containing the script to evaluate.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout to apply when checking the file against the
        /// active policy.
        /// </param>
        /// <param name="flags">
        /// The optional shell flags controlling when fallback to a normal
        /// evaluation is permitted.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script file, or
        /// information about any error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode EvaluateEncodedFile(
            Interpreter interpreter, /* in */
            Encoding encoding,       /* in */
            string fileName,         /* in */
            int? timeout,            /* in: OPTIONAL */
            ShellFlags? flags,       /* in: OPTIONAL */
            ref Result result        /* out */
            )
        {
            int errorLine = 0;

            return EvaluateEncodedFile(
                interpreter, encoding, fileName, timeout,
                flags, ref result, ref errorLine);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the script contained in the specified file, using the
        /// specified character encoding, under the certificate trust policy,
        /// optionally falling back to a normal (i.e. non-trusted) evaluation
        /// depending on the supplied shell flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which the script file is to be evaluated.
        /// </param>
        /// <param name="encoding">
        /// The character encoding used to read the script file, or null to
        /// use the default encoding.
        /// </param>
        /// <param name="fileName">
        /// The name of the file containing the script to evaluate.
        /// </param>
        /// <param name="timeout">
        /// The optional timeout to apply when checking the file against the
        /// active policy.
        /// </param>
        /// <param name="flags">
        /// The optional shell flags controlling when fallback to a normal
        /// evaluation is permitted.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the result of evaluating the script file, or
        /// information about any error.
        /// </param>
        /// <param name="errorLine">
        /// Upon return, receives the line number where an error occurred, if
        /// any.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode EvaluateEncodedFile(
            Interpreter interpreter, /* in */
            Encoding encoding,       /* in */
            string fileName,         /* in */
            int? timeout,            /* in: OPTIONAL */
            ShellFlags? flags,       /* in: OPTIONAL */
            ref Result result,       /* out */
            ref int errorLine        /* out */
            )
        {
            if (interpreter == null)
            {
                result = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (!interpreter.IsSafe())
            {
                if (CertificateSharedOps.HasFlags(
                        flags, ShellFlags.FallbackOnUnsafe, true))
                {
                    goto fallback;
                }
                else
                {
                    result = "interpreter is not safe";
                    return ReturnCode.Error;
                }
            }
            else if (!IsTrustSignedOnly(interpreter, PolicyType.File, false))
            {
                if (CertificateSharedOps.HasFlags(
                        flags, ShellFlags.FallbackOnNoPolicy, true))
                {
                    goto fallback;
                }
                else
                {
                    result = "file policy is not set to trust signed only";
                    return ReturnCode.Error;
                }
            }

            /* IGNORED */
            Interlocked.Increment(ref trustedLevels);

            try
            {
                IPolicyContext policyContext = null;
                Result localResult = null;

                if (CertificateSharedOps.CheckFileAgainstPolicy(
                        interpreter, encoding, fileName, timeout,
                        ref policyContext, ref localResult) == ReturnCode.Ok)
                {
                    if (policyContext.IsApproved())
                    {
                        return interpreter.EvaluateTrustedFile(
                            encoding, fileName, Constants.FileTrustFlags,
                            ref result, ref errorLine);
                    }
                    else if (policyContext.IsDenied())
                    {
                        if (CertificateSharedOps.HasFlags(
                                flags, ShellFlags.FallbackOnDenied, true))
                        {
                            goto fallback;
                        }
                        else
                        {
                            if (localResult != null)
                                result = localResult;
                            else
                                result = "file denied by policy";
                        }
                    }
                    else
                    {
                        //
                        // NOTE: At this point, the policy context is either
                        //       officially "undecided" (one or more neutral
                        //       votes) or "none" (no votes whatsoever).  In
                        //       either of these cases, just fallback to the
                        //       normal (i.e. non-trusted) script evaluation.
                        //
                        if (CertificateSharedOps.HasFlags(
                                flags, ShellFlags.FallbackOnNeutral, true))
                        {
                            goto fallback;
                        }
                        else
                        {
                            result = "file not approved by policy";
                            return ReturnCode.Error;
                        }
                    }
                }
                else if (localResult != null)
                {
                    if (CertificateSharedOps.HasFlags(
                            flags, ShellFlags.FallbackOnFailure, true))
                    {
                        goto fallback;
                    }
                    else
                    {
                        result = localResult;
                    }
                }
                else
                {
                    if (CertificateSharedOps.HasFlags(
                            flags, ShellFlags.FallbackOnFailure, true))
                    {
                        goto fallback;
                    }
                    else
                    {
                        result = "file policy callback failed";
                    }
                }
            }
            catch (Exception e)
            {
                //
                // NOTE: Cannot fallback here because the script file
                //       MAY have been partially evaluated.
                //
                result = e;
            }
            finally
            {
                /* IGNORED */
                Interlocked.Decrement(ref trustedLevels);
            }

            return ReturnCode.Error;

        fallback:

            //
            // NOTE: Fallback to the normal (i.e. non-trusted) script
            //       file evaluation.  By default, this is used only
            //       when an interpreter is "unsafe" and/or its file
            //       policy is not configured; however, it can now be
            //       used when some kinds of failure are encountered.
            //
            EngineFlags engineFlags = EngineFlags.None;

            if (CertificateSharedOps.HasFlags(
                    flags, ShellFlags.NoPoliciesOnFallback, true))
            {
                engineFlags |= Constants.FallbackEngineFlags;
            }

            /* IGNORED */
            Interlocked.Increment(ref fallbackLevels);

            try
            {
                return interpreter.EvaluateFile(
                    encoding, fileName, engineFlags, ref result,
                    ref errorLine);
            }
            finally
            {
                /* IGNORED */
                Interlocked.Decrement(ref fallbackLevels);
            }
        }
        #endregion
    }
}
