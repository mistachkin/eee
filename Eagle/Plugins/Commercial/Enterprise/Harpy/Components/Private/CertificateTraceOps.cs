/*
 * CertificateTraceOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;
using Utility = Eagle._Components.Public.Utility;
using SharedOps = Licensing.Components.Private.CertificateSharedOps;
using DataOps = Licensing.Components.Private.CertificateDataOps;

using InterpreterPair = Eagle._Interfaces.Public.IAnyPair<
    Eagle._Components.Public.Interpreter, Eagle._Interfaces.Public.IClientData>;

using MutableInterpreterPair = Eagle._Interfaces.Public.IMutableAnyPair<
    Eagle._Components.Public.Interpreter, Eagle._Interfaces.Public.IClientData>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides private helper methods for managing the Harpy trace log
    /// file and the elevated trace priorities used by the certificate
    /// licensing subsystem.
    /// </summary>
    [ObjectId("34282b4c-0774-42bb-9dad-b0ec661c3d69")]
    internal static class CertificateTraceOps
    {
        #region Private Constants
        //
        // NOTE: This is the file name prefix to use when generating a unique
        //       trace log file name (i.e. when one was not manually setup by
        //       the user).
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The file name prefix used when generating a unique trace log
        /// file name automatically.
        /// </summary>
        private static string autoTraceFileNamePrefix = "harpy-auto-trace-";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the format string used to build the trace log file
        //       name.  The inserted parameters are the process identifier,
        //       the application domain identifier, the thread identifier,
        //       an integer sequence number, and the file extension.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The format string used to build the fallback trace log file
        /// name when a unique temporary file name cannot be found.
        /// </summary>
        private static string fallbackTraceFileNameFormat =
            "Harpy_{0}_{1}_{2}_{3}{4}";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the value to be used instead of the specified trace
        //       base priorities passed to the priority parameter of the core
        //       library tracing subsystem.  For this value to be useful, it
        //       should normally be set to "Highest".
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The base trace priority to use instead of the one specified
        /// when passing the priority to the core library tracing
        /// subsystem.
        /// </summary>
        private static TracePriority setBasePriority =
            TracePriority.Highest; /* EXEMPT */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the mask of trace priorities that should be enabled
        //       via the core library tracing subsystem while the log file
        //       is active, etc.  For this value to be useful, it should
        //       normally be set to "HasPrioritiesMask".
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The mask of trace priorities to enable via the core library
        /// tracing subsystem while the log file is active.
        /// </summary>
        private static TracePriority setBasePriorities =
            TracePriority.HasPrioritiesMask;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the default sets of extra trace priorities to add
        //       or remove the priority parameter passed to the core library
        //       tracing subsystem.  In general, the "add" set should be the
        //       set that enables all trace priorities and types (i.e. as it
        //       makes using the log file more worthwhile).
        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// The default set of extra trace priorities to add to the
        /// priority passed to the core library tracing subsystem.
        /// </summary>
        private static TracePriority addExtraPriority = TracePriority.None;
        /// <summary>
        /// The default set of extra trace priorities to remove from the
        /// priority passed to the core library tracing subsystem.
        /// </summary>
        private static TracePriority removeExtraPriority = TracePriority.None;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When this flag is non-zero, the core tracing subsystem will
        //       be forcibly reset when the text writer managed by this class
        //       has been disabled.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// Non-zero if the core tracing subsystem should be forcibly
        /// reset when the managed text writer has been disabled.
        /// </summary>
        private static bool ResetTracingOnDisable = true;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: These are purposely not read-only.
        //
        /// <summary>
        /// The format string used to emit the opening tag that wraps an
        /// enhanced stack trace.
        /// </summary>
        private static string StackTraceStartFormat = "<{0}>";
        /// <summary>
        /// The format string used to emit the closing tag that wraps an
        /// enhanced stack trace.
        /// </summary>
        private static string StackTraceEndFormat = "</{0}>";
        /// <summary>
        /// The tag name used when wrapping an enhanced stack trace.
        /// </summary>
        private static string StackTraceName = "MaybeEnhanceWithStackTrace";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This is used to synchronize access to the "basePriority" and
        //       "extraPriority" static fields.
        //
        /// <summary>
        /// Used to synchronize access to the static fields that hold the
        /// base and extra trace priorities.
        /// </summary>
        private static object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This counter is used to keep track of how many nested calls
        //       there have been to enable the text writer.  When this count
        //       hits one (1) the base and extra priority fields will be set
        //       to enable all output.  When this count hits zero (0), these
        //       changes will be undone.
        //
        /// <summary>
        /// The count of nested calls that have enabled the text writer,
        /// used to apply the priority changes only when first enabled.
        /// </summary>
        private static int enabledCount;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The base priority to use instead of the specified one when
        //       passing the priority parameter to the core library tracing
        //       subsystem.
        //
        /// <summary>
        /// The base priority to use instead of the specified one when
        /// passing the priority to the core library tracing subsystem.
        /// </summary>
        private static TracePriority basePriority = TracePriority.None;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The extra trace priorities to add to the priority parameter
        //       passed to the core library tracing subsystem.  In general,
        //       this should only be set when the trace log file is enabled.
        //
        /// <summary>
        /// The extra trace priorities to add to the priority passed to
        /// the core library tracing subsystem.
        /// </summary>
        private static TracePriority extraPriority = TracePriority.None;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: *HACK* When this is non-zero, global trace priorities will
        //       be adjusted while the elevated trace priorities are enabled.
        //
        /// <summary>
        /// Non-zero if global trace priorities should be adjusted while
        /// the elevated trace priorities are enabled.
        /// </summary>
        private static bool adjustPriorities = true;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: *HACK* When this is non-zero, trace limits will be disabled
        //       while the elevated trace priorities are enabled.
        //
        /// <summary>
        /// Non-zero if trace limits should be disabled while the elevated
        /// trace priorities are enabled.
        /// </summary>
        private static bool adjustLimits = true;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: *HACK* When this is non-zero, trace messages will be emitted
        //       when the trace file is enabled and/or disabled.
        //
        /// <summary>
        /// Non-zero if trace messages should be emitted when the trace
        /// file is enabled and/or disabled.
        /// </summary>
        private static bool ultraVerbose = false;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: When this (static) field is non-zero, the tracing subsystem
        //       will be forcibly enabled (and subsequently disabled) by all
        //       the policy implementation methods.
        //
        /// <summary>
        /// The flags that control how the tracing subsystem is forcibly
        /// enabled and disabled by the policy implementation methods.
        /// </summary>
        private static PolicyTraceFlags policyFlags = PolicyTraceFlags.Default;

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: When this (static) field is non-zero, the log file name will
        //       always be set to something, even if a unique temporary file
        //       name cannot be found.  Prior to beta 50, it was possible to
        //       hit an issue with isolated application domains, apparently
        //       due to issues marshalling byte array parameters not declared
        //       as "ref".
        //
        /// <summary>
        /// Non-zero if the log file name should always be set to a
        /// fallback value when a unique temporary file name cannot be
        /// found.
        /// </summary>
        private static bool useFallbackLogFileName = true; /* TODO: Good default? */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: When this (static) field is non-zero, the log file name will
        //       be checked prior to being used.  It cannot be null, an empty
        //       string, a relative path, or reside in a directory that does
        //       not exist.
        //
        /// <summary>
        /// Non-zero if the log file name should be validated before being
        /// used.
        /// </summary>
        private static bool failSafeLogFileName = false; /* TODO: Good default? */

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: When this (static) field is zero, the log file will have all
        //       the priority flags included for each log entry.
        //
        /// <summary>
        /// Non-zero if each log entry should include only the base
        /// priority flags instead of all of them.
        /// </summary>
        private static bool basePriorityOnly = true; /* TODO: Good default? */
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Policy Implementation "Forced" Tracing (Special)
        //
        // WARNING: For use by the LoadAndProcess method only.
        //
        /// <summary>
        /// Determines whether the tracing subsystem should be forcibly
        /// enabled for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if tracing should be forcibly enabled.
        /// </returns>
        public static bool ShouldForceEnableForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Enable);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether an automatic trace log file should be
        /// forcibly used for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if an automatic trace log file should be used.
        /// </returns>
        private static bool ShouldForceAutoFileForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.AutoFile);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the trace log file should be forcibly
        /// appended to for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the trace log file should be appended to.
        /// </returns>
        private static bool ShouldForceAppendForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Append);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the trace log file should be forcibly
        /// opened in shared mode for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the trace log file should be shared.
        /// </returns>
        private static bool ShouldForceSharedForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Shared);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the trace priority overrides should be
        /// forcibly applied for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the trace priority overrides should be applied.
        /// </returns>
        private static bool ShouldForceTracingForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Tracing);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: For use by the InterpreterCallbackPrologue method only.
        //
        /// <summary>
        /// Determines whether the trace text writer should be forcibly
        /// cloned for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the trace text writer should be cloned.
        /// </returns>
        public static bool ShouldForceCloneForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Clone);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: For use only by the policy implementation methods in the
        //          CertificatePolicyOps class.
        //
        /// <summary>
        /// Determines whether full tracing should be forcibly enabled for
        /// policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if full tracing should be enabled.
        /// </returns>
        public static bool ShouldForceFullForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Full);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the core tracing subsystem should be
        /// forcibly reset for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the tracing subsystem should be reset.
        /// </returns>
        private static bool ShouldForceResetForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Reset);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the base trace priority override should be
        /// forcibly applied for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the base trace priority override should be applied.
        /// </returns>
        private static bool ShouldForcePriorityForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Priority);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the global trace priorities should be
        /// forcibly adjusted for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the global trace priorities should be adjusted.
        /// </returns>
        private static bool ShouldForcePrioritiesForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Priorities);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the trace limits should be forcibly
        /// adjusted for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the trace limits should be adjusted.
        /// </returns>
        private static bool ShouldForceLimitsForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Limits);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the trace output should be forcibly
        /// enhanced for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if the trace output should be enhanced.
        /// </returns>
        private static bool ShouldForceEnhanceForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Enhance);
        }

        ///////////////////////////////////////////////////////////////////////

#if POLICY_TRACE
        /// <summary>
        /// Determines whether per-interpreter policy tracing should be
        /// forcibly enabled for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if per-interpreter policy tracing should be enabled.
        /// </returns>
        private static bool ShouldForceInterpreterForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Interpreter);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether global policy tracing should be forcibly
        /// enabled for policy purposes.
        /// </summary>
        /// <returns>
        /// Non-zero if global policy tracing should be enabled.
        /// </returns>
        private static bool ShouldForceGlobalForPolicy() /* CORE */
        {
            return HavePolicyFlags(PolicyTraceFlags.Global);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the configured policy trace flags include
        /// all of the specified flags.
        /// </summary>
        /// <param name="hasFlags">
        /// The policy trace flags to check for.
        /// </param>
        /// <returns>
        /// Non-zero if all of the specified flags are present.
        /// </returns>
        private static bool HavePolicyFlags( /* CORE */
            PolicyTraceFlags hasFlags /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                return SharedOps.HasFlags(policyFlags, hasFlags, true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if POLICY_TRACE
        /// <summary>
        /// Sets the policy tracing flag on the specified interpreter, if
        /// it is available.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to modify.
        /// </param>
        /// <param name="enable">
        /// Non-zero to enable policy tracing.
        /// </param>
        private static void MaybeForceForPolicy( /* CORE? */
            Interpreter interpreter, /* in */
            bool enable              /* in */
            )
        {
            if (interpreter == null)
                return;

            interpreter.PolicyTrace = enable;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the global policy tracing flag.
        /// </summary>
        /// <param name="enable">
        /// Non-zero to enable policy tracing.
        /// </param>
        private static void MaybeForceForPolicy( /* CORE? */
            bool enable /* in */
            )
        {
            Utility.SetPolicyTrace(enable);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Replaces the configured policy trace flags with the specified
        /// flags, when non-null, returning the previous flags.
        /// </summary>
        /// <param name="flags">
        /// The new policy trace flags, or null to leave them unchanged.
        /// </param>
        /// <returns>
        /// The previous policy trace flags.
        /// </returns>
        private static PolicyTraceFlags? MaybeForceForPolicy( /* CORE? */
            PolicyTraceFlags? flags /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                PolicyTraceFlags oldPolicyFlags = policyFlags;

                if (flags != null)
                    policyFlags = (PolicyTraceFlags)flags;

                return oldPolicyFlags;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: For [certificate policytrace] sub-command use only.  This
        //          method should not be called from anywhere else.
        //
        /// <summary>
        /// Replaces the configured policy trace flags and applies the
        /// resulting core library configuration, returning the previous
        /// flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to apply the configuration to.
        /// </param>
        /// <param name="flags">
        /// The new policy trace flags, or null to leave them unchanged.
        /// </param>
        /// <returns>
        /// The previous policy trace flags.
        /// </returns>
        public static PolicyTraceFlags? MaybeForceForPolicy( /* CORE? */
            Interpreter interpreter, /* in */
            PolicyTraceFlags? flags  /* in */
            )
        {
            PolicyTraceFlags? result;
            bool enable;

            ///////////////////////////////////////////////////////////////////

            #region Mutate Static Class Data (Required)
            result = MaybeForceForPolicy(flags);
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Handle Common Flags
            enable = ShouldForceEnableForPolicy();
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Mutate Core Library Configuration (Optional)
#if POLICY_TRACE
            if (ShouldForceInterpreterForPolicy())
            {
                /* NO RESULT */
                MaybeForceForPolicy(interpreter, enable);
            }

            ///////////////////////////////////////////////////////////////////

            if (ShouldForceGlobalForPolicy())
            {
                /* NO RESULT */
                MaybeForceForPolicy(enable);
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Mutate Priority Overrides (Optional)
            if (ShouldForcePriorityForPolicy())
            {
                /* NO RESULT */
                MaybeForcePriority(null, true, enable);
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Mutate Limits (Optional)
            if (ShouldForceLimitsForPolicy())
            {
                /* NO RESULT */
                MaybeAdjustLimits(interpreter, null, true, enable);
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the name of the environment variable used to store the
        /// automatically generated trace log file name.
        /// </summary>
        /// <param name="envVarName">
        /// Upon return, receives the environment variable name.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void GetFileEnvVarName( /* CORE */
            ref string envVarName /* out */
            )
        {
            envVarName = SharedOps.GetEnvVarName(
                Constants.CertificateTraceFileEnvVarName, null);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // WARNING: For use by the LoadAndProcess method only.
        //
        /// <summary>
        /// Adjusts the specified trace priority by applying the current
        /// base and extra priority overrides.
        /// </summary>
        /// <param name="priority">
        /// The trace priority to adjust, in place.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        public static void MaybeAdjustPriority( /* CORE */
            ref TracePriority priority /* in, out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (basePriority != TracePriority.None)
                {
                    priority &= ~TracePriority.AnyPriorityMask;
                    priority |= basePriority;
                }

                //
                // HACK: Finally, also add any "extra" priority
                //       flags that may be present to enhance
                //       the resulting trace output (i.e. more
                //       verbosity, etc).
                //
                priority |= extraPriority;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies or reverts the trace priority overrides, saving and
        /// restoring the original priority as needed.
        /// </summary>
        /// <param name="enable">
        /// Non-zero to apply the overrides, zero to revert them.
        /// </param>
        /// <param name="priority">
        /// The trace priority to adjust, in place.
        /// </param>
        /// <param name="savedPriority">
        /// The saved trace priority to restore from or store into.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void MaybeAdjustPriority( /* CORE */
            bool enable,                    /* in */
            ref TracePriority priority,     /* in, out */
            ref TracePriority savedPriority /* in, out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (enable)
                {
                    //
                    // HACK: Reset the base priority of the trace
                    //       message by removing any existing one
                    //       and adding the new one.
                    //
                    savedPriority = priority;
                    MaybeAdjustPriority(ref priority);
                }
                else
                {
                    priority = savedPriority;
                    savedPriority = TracePriority.None;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Begins adjusting the global and per-interpreter trace
        /// priorities, saving their original values.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trace priorities may be adjusted.
        /// </param>
        /// <param name="forcePriorities">
        /// Non-zero to force the trace priorities to be adjusted.
        /// </param>
        /// <param name="savedPriorities1">
        /// Upon return, receives the saved global trace priorities.
        /// </param>
        /// <param name="savedPriorities2">
        /// Upon return, receives the saved interpreter trace priorities.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void BeginMaybeAdjustPriorities( /* CORE */
            Interpreter interpreter,             /* in */
            bool forcePriorities,                /* in */
            ref TracePriority? savedPriorities1, /* out */
            ref TracePriority? savedPriorities2  /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (forcePriorities || adjustPriorities)
                {
                    savedPriorities1 = Utility.GetTracePriorities();

                    Utility.AdjustTracePriorities(
                        setBasePriorities, true);

                    if ((interpreter != null) &&
                        Utility.IsTransparentProxy(interpreter))
                    {
                        savedPriorities2 = interpreter.GetTracePriorities();

                        interpreter.AdjustTracePriorities(
                            setBasePriorities, true);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ends adjusting the global and per-interpreter trace
        /// priorities, restoring their saved values.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trace priorities may be restored.
        /// </param>
        /// <param name="forcePriorities">
        /// Non-zero to force the trace priorities to be restored.
        /// </param>
        /// <param name="savedPriorities1">
        /// The saved global trace priorities to restore, then cleared.
        /// </param>
        /// <param name="savedPriorities2">
        /// The saved interpreter trace priorities to restore, then
        /// cleared.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void EndMaybeAdjustPriorities( /* CORE */
            Interpreter interpreter,             /* in */
            bool forcePriorities,                /* in */
            ref TracePriority? savedPriorities1, /* in, out */
            ref TracePriority? savedPriorities2  /* in, out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (forcePriorities || adjustPriorities)
                {
                    if (savedPriorities1 != null)
                    {
                        Utility.SetTracePriorities(
                            (TracePriority)savedPriorities1);

                        savedPriorities1 = null;
                    }

                    if ((savedPriorities2 != null) &&
                        (interpreter != null) &&
                        Utility.IsTransparentProxy(interpreter))
                    {
                        interpreter.SetTracePriorities(
                            (TracePriority)savedPriorities2);

                        savedPriorities2 = null;
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds or removes the specified extra trace priority from the
        /// current extra priority mask.
        /// </summary>
        /// <param name="priority">
        /// The extra trace priority to add or remove.
        /// </param>
        /// <param name="enable">
        /// Non-zero to add the priority, zero to remove it.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void AdjustExtraPriority( /* CORE */
            TracePriority priority, /* in */
            bool enable             /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (enable)
                    extraPriority |= priority;
                else
                    extraPriority &= ~priority;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjusts the global and per-interpreter trace limits when
        /// enabling or disabling elevated tracing.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trace limits may be adjusted.
        /// </param>
        /// <param name="policy">
        /// The execution policy associated with the request.  Not used.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the trace limits to be adjusted.
        /// </param>
        /// <param name="enable">
        /// Non-zero to disable the trace limits, zero to restore them.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void MaybeAdjustLimits( /* CORE */
            Interpreter interpreter, /* in */
            ExecutionPolicy? policy, /* in: NOT USED */
            bool force,              /* in */
            bool enable              /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (force || adjustLimits)
                {
                    /* IGNORED */
                    Utility.MaybeAdjustTraceLimits(enable);

                    if ((interpreter != null) &&
                        Utility.IsTransparentProxy(interpreter))
                    {
                        /* IGNORED */
                        interpreter.MaybeAdjustTraceLimits(enable);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies or reverts the base trace priority override based on
        /// the specified execution policy.
        /// </summary>
        /// <param name="policy">
        /// The execution policy that may request forced tracing.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the priority override to be applied.
        /// </param>
        /// <param name="enable">
        /// Non-zero to apply the override, zero to revert it.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void MaybeForcePriority( /* CORE */
            ExecutionPolicy? policy, /* in */
            bool force,              /* in */
            bool enable              /* in */
            )
        {
            TracePriority? savedBasePriority = null;

            MaybeForcePriority(
                policy, force, enable, ref savedBasePriority);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies or reverts the base trace priority override, saving
        /// and restoring the original base priority as needed.
        /// </summary>
        /// <param name="policy">
        /// The execution policy that may request forced tracing.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the priority override to be applied.
        /// </param>
        /// <param name="enable">
        /// Non-zero to apply the override, zero to revert it.
        /// </param>
        /// <param name="savedBasePriority">
        /// The saved base trace priority to restore from or store into.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void MaybeForcePriority( /* CORE */
            ExecutionPolicy? policy,             /* in */
            bool force,                          /* in */
            bool enable,                         /* in */
            ref TracePriority? savedBasePriority /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (force || Utility.HasFlags(
                        policy, ExecutionPolicy.ForceTracing, true))
                {
                    if (enable)
                    {
                        if (savedBasePriority == null)
                            savedBasePriority = basePriority;

                        basePriority = setBasePriority;

                        AdjustExtraPriority(addExtraPriority, true);
                        AdjustExtraPriority(removeExtraPriority, false);
                    }
                    else
                    {
                        AdjustExtraPriority(removeExtraPriority, true);
                        AdjustExtraPriority(addExtraPriority, false);

                        if (savedBasePriority != null)
                        {
                            basePriority = (TracePriority)savedBasePriority;
                            savedBasePriority = null;
                        }
                        else
                        {
                            basePriority = TracePriority.None;
                        }
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the trace priority mask used to enhance trace output with
        /// troubleshooting information, allowing an environment variable
        /// override.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to parse the override value.
        /// </param>
        /// <param name="envVarName">
        /// The name of the environment variable that may override the
        /// mask.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to parse the override value.  Optional.
        /// </param>
        /// <param name="priority">
        /// Upon return, receives the troubleshooting trace priority mask.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void GetEnhanceWithTroubleshootingMask(
            Interpreter interpreter,   /* in */
            string envVarName,         /* in */
            CultureInfo cultureInfo,   /* in: OPTIONAL */
            ref TracePriority priority /* in, out */
            )
        {
            TracePriority localPriority = TracePriority.TroubleshootingMask;
            string newValue = Configuration.GetVariable(envVarName);

            if (!String.IsNullOrEmpty(newValue))
            {
                object enumValue;
                Result error = null;

                enumValue = Utility.TryParseFlagsEnum(
                    interpreter, typeof(TracePriority),
                    localPriority.ToString(), newValue,
                    cultureInfo, true, true, true, ref error);

                if (enumValue is TracePriority)
                    localPriority = (TracePriority)enumValue;
            }

            priority = localPriority;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Enhances or reduces the specified trace priority with the
        /// troubleshooting mask when verbose tracing is in effect.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to obtain and parse the mask.
        /// </param>
        /// <param name="envVarName">
        /// The name of the environment variable that may override the
        /// mask.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to parse the override value.  Optional.
        /// </param>
        /// <param name="force">
        /// Non-zero to force the troubleshooting mask to be applied.
        /// </param>
        /// <param name="enhance">
        /// Non-zero to add the mask, zero to remove it.
        /// </param>
        /// <param name="priority">
        /// The trace priority to adjust, in place.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void MaybeEnhanceWithTroubleshooting( /* CORE? */
            Interpreter interpreter,   /* in */
            string envVarName,         /* in */
            CultureInfo cultureInfo,   /* in: OPTIONAL */
            bool force,                /* in */
            bool enhance,              /* in */
            ref TracePriority priority /* in, out */
            )
        {
            ExecutionPolicy policy = CertificatePolicyOps.GetPolicy(
                PolicyType.Trace);

            if (force || Utility.HasFlags(
                    policy, ExecutionPolicy.VerboseTracing, true))
            {
                TracePriority enhancePriority = TracePriority.None;

                GetEnhanceWithTroubleshootingMask(
                    interpreter, envVarName, cultureInfo,
                    ref enhancePriority);

                if (enhance)
                    priority |= enhancePriority;
                else
                    priority &= ~enhancePriority;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the trace text writer from the source interpreter to
        /// the target interpreter, sharing the same writer instance.
        /// </summary>
        /// <param name="targetInterpreter">
        /// The interpreter to copy the trace text writer to.
        /// </param>
        /// <param name="sourceInterpreter">
        /// The interpreter to copy the trace text writer from.
        /// </param>
        /// <param name="clientData">
        /// The optional client data for the operation.  Not used.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the error message on failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode CopyTextWriter( /* CORE? */
            Interpreter targetInterpreter, /* in */
            Interpreter sourceInterpreter, /* in */
            IClientData clientData,        /* in: NOT USED */
            ref Result result              /* out */
            )
        {
            if (sourceInterpreter == null)
            {
                result = "invalid source interpreter";
                return ReturnCode.Error;
            }

            if (targetInterpreter == null)
            {
                result = "invalid target interpreter";
                return ReturnCode.Error;
            }

            TextWriter sourceTextWriter = sourceInterpreter.TraceTextWriter;

            if (sourceTextWriter == null)
                return ReturnCode.Ok;

            TextWriter targetTextWriter = targetInterpreter.TraceTextWriter;

            if (targetTextWriter != null)
                return ReturnCode.Ok;

            targetInterpreter.TraceTextWriterOwned = false;
            targetInterpreter.TraceTextWriter = sourceTextWriter;

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clones the trace text writer from the source interpreter into
        /// a new writer owned by the target interpreter.
        /// </summary>
        /// <param name="targetInterpreter">
        /// The interpreter to create the cloned trace text writer for.
        /// </param>
        /// <param name="sourceInterpreter">
        /// The interpreter to clone the trace text writer from.
        /// </param>
        /// <param name="clientData">
        /// The optional client data for the operation.  Not used.
        /// </param>
        /// <param name="result">
        /// Upon return, receives the error message on failure.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode CloneTextWriter( /* CORE? */
            Interpreter targetInterpreter, /* in */
            Interpreter sourceInterpreter, /* in */
            IClientData clientData,        /* in: NOT USED */
            ref Result result              /* out */
            )
        {
            if (sourceInterpreter == null)
            {
                result = "invalid source interpreter";
                return ReturnCode.Error;
            }

            if (targetInterpreter == null)
            {
                result = "invalid target interpreter";
                return ReturnCode.Error;
            }

            TextWriter sourceTextWriter = sourceInterpreter.TraceTextWriter;

            if (sourceTextWriter == null)
                return ReturnCode.Ok;

            TextWriter targetTextWriter = targetInterpreter.TraceTextWriter;

            if (targetTextWriter != null)
                return ReturnCode.Ok;

#if DEBUG_TRACE
            try
            {
                OpenTextWriter(
                    targetInterpreter, sourceTextWriter as TraceStreamWriter,
                    ref targetTextWriter);

                if (targetTextWriter == null)
                {
                    result = "could not open cloned trace stream writer";
                    return ReturnCode.Error;
                }
            }
            catch (Exception e)
            {
                result = e;
                return ReturnCode.Error;
            }

            return ReturnCode.Ok;
#else
            result = "not implemented";
            return ReturnCode.Error;
#endif
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Modifies the specified execution policy based on an
        /// environment variable, allowing only trace-related flags to be
        /// added.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to parse the override value.
        /// </param>
        /// <param name="envVarName">
        /// The name of the environment variable that may modify the
        /// policy.
        /// </param>
        /// <param name="maskValues">
        /// The mask of flag values that are allowed to be changed.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to parse the override value.
        /// </param>
        /// <param name="policy">
        /// The execution policy to modify, in place.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        public static void MaybeChangeExecutionPolicy( /* CORE */
            Interpreter interpreter,    /* in */
            string envVarName,          /* in */
            string maskValues,          /* in */
            CultureInfo cultureInfo,    /* in */
            ref ExecutionPolicy? policy /* in, out */
            )
        {
            if (envVarName == null)
                return;

            string newValue = Configuration.GetVariable(envVarName);

            if (String.IsNullOrEmpty(newValue))
                return;

            ExecutionPolicy localPolicy = (policy != null) ?
                (ExecutionPolicy)policy : ExecutionPolicy.None;

            //
            // NOTE: When processing modifications to the execution policy
            //       value, do *not* allow values to be removed.  Also, do
            //       *not* allow any non-trace related values to be added.
            //
            object enumValue;
            Result error = null;

            enumValue = Utility.TryParseFlagsEnum(
                interpreter, typeof(ExecutionPolicy), localPolicy.ToString(),
                newValue, maskValues, String.Empty, cultureInfo, true, true,
                true, true, ref error);

            if (enumValue is ExecutionPolicy)
            {
                policy = (ExecutionPolicy)enumValue;
            }
            else
            {
                //
                // HACK: Failing to change the execution policy [per a user
                //       request] is a very high priority situation.
                //
                /* NO RESULT */
                DebugTrace(interpreter, String.Format(
                    "MaybeChangeExecutionPolicy: could not change execution " +
                    "policy, error = {0}", Utility.FormatWrapOrNull(error)),
                    typeof(CertificateTraceOps).Name, TracePriority.Highest, 0);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and initializes the trace client data used to enable
        /// or disable the core tracing subsystem.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the trace client data.
        /// </param>
        /// <param name="enable">
        /// Non-zero to configure the client data for enabling tracing.
        /// </param>
        /// <param name="traceClientData">
        /// Upon return, receives the created trace client data.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void CreateClientData( /* CORE */
            Interpreter interpreter,            /* in */
            bool enable,                        /* in */
            ref TraceClientData traceClientData /* out */
            )
        {
            traceClientData = new TraceClientData();
            traceClientData.Interpreter = interpreter;

            traceClientData.StateType = enable ?
                TraceStateType.SdkEnableMask : TraceStateType.SdkDisableMask;

            traceClientData.ForceEnabled = enable;
            traceClientData.ResetSystem = true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates the trace client data and processes it against the
        /// global and per-interpreter tracing subsystems.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the trace client data.
        /// </param>
        /// <param name="enable">
        /// Non-zero to configure the client data for enabling tracing.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void CreateAndProcessClientData( /* CORE */
            Interpreter interpreter, /* in */
            bool enable              /* in */
            )
        {
            TraceClientData traceClientData = null;

            CreateClientData(
                interpreter, enable, ref traceClientData);

            Result result = null; /* REUSED */

            if (Utility.ProcessTraceClientData(
                    traceClientData, ref result) != ReturnCode.Ok)
            {
                /* NO RESULT */
                DebugTrace(interpreter, String.Format(
                    "CreateAndProcessClientData: #1, error = {0}",
                    Utility.FormatWrapOrNull(result)),
                    typeof(CertificateTraceOps).Name,
                    TracePriority.Highest, 0);
            }

            if ((interpreter != null) &&
                Utility.IsTransparentProxy(interpreter))
            {
                result = null;

                if (interpreter.ProcessTraceClientData(
                        traceClientData, ref result) != ReturnCode.Ok)
                {
                    /* NO RESULT */
                    DebugTrace(interpreter, String.Format(
                        "CreateAndProcessClientData: #2, error = {0}",
                        Utility.FormatWrapOrNull(result)),
                        typeof(CertificateTraceOps).Name,
                        TracePriority.Highest, 0);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds a fallback trace log file name, optionally within the
        /// specified directory.
        /// </summary>
        /// <param name="directory">
        /// The directory to place the log file in.  Optional.
        /// </param>
        /// <param name="fileName">
        /// Upon return, receives the fallback log file name.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void GetFallbackLogFileName( /* CORE */
            string directory,   /* in: OPTIONAL */
            ref string fileName /* out */
            )
        {
            string localFileName = String.Format(
                fallbackTraceFileNameFormat,
                Utility.GetCurrentProcessId(),
                Utility.GetCurrentAppDomainId(),
                Utility.GetCurrentThreadId(),
                DataOps.FormatHexadecimal(
                    Utility.GetRandomNumber(), false),
                FileExtension.Log);

            fileName = !String.IsNullOrEmpty(directory) ?
                Path.Combine(directory, localFileName) :
                localFileName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjusts the trace priorities and limits when enabling or
        /// disabling tracing, using the configured policy trace flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose tracing configuration may be adjusted.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to parse any override values.  Optional.
        /// </param>
        /// <param name="policy">
        /// The execution policy that may request forced tracing.
        /// </param>
        /// <param name="enable">
        /// Non-zero to apply the adjustments, zero to revert them.
        /// </param>
        /// <param name="savedBasePriority">
        /// Upon return, receives the saved base trace priority.
        /// </param>
        /// <param name="savedPriorities1">
        /// Upon return, receives the saved global trace priorities.
        /// </param>
        /// <param name="savedPriorities2">
        /// Upon return, receives the saved interpreter trace priorities.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        public static void AdjustPrioritiesAndLimits( /* CORE */
            Interpreter interpreter,              /* in */
            CultureInfo cultureInfo,              /* in: OPTIONAL */
            ExecutionPolicy? policy,              /* in */
            bool enable,                          /* in */
            ref TracePriority? savedBasePriority, /* out */
            ref TracePriority? savedPriorities1,  /* out */
            ref TracePriority? savedPriorities2   /* out */
            )
        {
            AdjustPrioritiesAndLimits(
                interpreter, cultureInfo, policy, enable,
                ShouldForceTracingForPolicy(),
                ShouldForcePrioritiesForPolicy(),
                ShouldForceLimitsForPolicy(),
                ShouldForceEnhanceForPolicy(),
                ref savedBasePriority,
                ref savedPriorities1,
                ref savedPriorities2);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adjusts the trace priorities and limits when enabling or
        /// disabling tracing, using the specified force flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose tracing configuration may be adjusted.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to parse any override values.  Optional.
        /// </param>
        /// <param name="policy">
        /// The execution policy that may request forced tracing.
        /// </param>
        /// <param name="enable">
        /// Non-zero to apply the adjustments, zero to revert them.
        /// </param>
        /// <param name="forceTracing">
        /// Non-zero to force the trace priority override to be applied.
        /// </param>
        /// <param name="forcePriorities">
        /// Non-zero to force the trace priorities to be adjusted.
        /// </param>
        /// <param name="forceLimits">
        /// Non-zero to force the trace limits to be adjusted.
        /// </param>
        /// <param name="forceEnhance">
        /// Non-zero to force the trace output to be enhanced.
        /// </param>
        /// <param name="savedBasePriority">
        /// Upon return, receives the saved base trace priority.
        /// </param>
        /// <param name="savedPriorities1">
        /// Upon return, receives the saved global trace priorities.
        /// </param>
        /// <param name="savedPriorities2">
        /// Upon return, receives the saved interpreter trace priorities.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void AdjustPrioritiesAndLimits( /* CORE */
            Interpreter interpreter,              /* in */
            CultureInfo cultureInfo,              /* in: OPTIONAL */
            ExecutionPolicy? policy,              /* in */
            bool enable,                          /* in */
            bool forceTracing,                    /* in: via PolicyTraceFlags */
            bool forcePriorities,                 /* in: via PolicyTraceFlags */
            bool forceLimits,                     /* in: via PolicyTraceFlags */
            bool forceEnhance,                    /* in: via PolicyTraceFlags */
            ref TracePriority? savedBasePriority, /* out */
            ref TracePriority? savedPriorities1,  /* out */
            ref TracePriority? savedPriorities2   /* out */
            )
        {
            //
            // NOTE: The "ForceTracing" and "VerboseTracing" execution
            //       policy flags are used by both of the primary code
            //       paths in this method.
            //
            bool tracing = forceTracing || Utility.HasFlags(
                policy, ExecutionPolicy.ForceTracing, true);

            bool enhance = forceEnhance || Utility.HasFlags(
                policy, ExecutionPolicy.VerboseTracing, true);

            //
            // NOTE: If needed, attempt to obtain priority mask used to
            //       "enhance" the trace output (i.e. more verbosity);
            //       This should be done here because it may be used by
            //       more than one place (see below).
            //
            TracePriority enhancePriority = TracePriority.None;

            if (enhance)
            {
                GetEnhanceWithTroubleshootingMask(interpreter,
                    Constants.VerboseTracePriorityEnvVarName,
                    cultureInfo, ref enhancePriority);
            }

            if (enable)
            {
                MaybeForcePriority(policy,
                    tracing, enable, ref savedBasePriority);

                if (enhance)
                    AdjustExtraPriority(enhancePriority, enable);

                BeginMaybeAdjustPriorities(interpreter,
                    forcePriorities, ref savedPriorities1,
                    ref savedPriorities2);

                MaybeAdjustLimits(
                    interpreter, policy, forceLimits, enable);
            }
            else
            {
                MaybeAdjustLimits(
                    interpreter, policy, forceLimits, enable);

                EndMaybeAdjustPriorities(interpreter,
                    forcePriorities, ref savedPriorities1,
                    ref savedPriorities2);

                if (enhance)
                    AdjustExtraPriority(enhancePriority, enable);

                MaybeForcePriority(policy,
                    tracing, enable, ref savedBasePriority);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables or disables the trace log text writer, using the
        /// configured policy trace flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trace text writer may be changed.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to parse any override values.  Optional.
        /// </param>
        /// <param name="policy">
        /// The execution policy that may request forced tracing.
        /// </param>
        /// <param name="enable">
        /// Non-zero to enable the text writer, zero to disable it.
        /// </param>
        /// <param name="wasEnabled">
        /// On input, whether the text writer was previously enabled; upon
        /// return, whether it is now enabled.
        /// </param>
        /// <param name="savedBasePriority">
        /// Upon return, receives the saved base trace priority.
        /// </param>
        /// <param name="savedPriorities1">
        /// Upon return, receives the saved global trace priorities.
        /// </param>
        /// <param name="savedPriorities2">
        /// Upon return, receives the saved interpreter trace priorities.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        public static void MaybeEnableOrDisableTextWriter( /* CORE */
            Interpreter interpreter,              /* in */
            CultureInfo cultureInfo,              /* in: OPTIONAL */
            ExecutionPolicy? policy,              /* in */
            bool enable,                          /* in */
            ref bool wasEnabled,                  /* in, out */
            ref TracePriority? savedBasePriority, /* out */
            ref TracePriority? savedPriorities1,  /* out */
            ref TracePriority? savedPriorities2   /* out */
            )
        {
            MaybeEnableOrDisableTextWriter(
                interpreter, cultureInfo, policy, enable,
                ShouldForceEnableForPolicy(),
                ShouldForceAutoFileForPolicy(),
                ShouldForceAppendForPolicy(),
                ShouldForceSharedForPolicy(),
                ShouldForceTracingForPolicy(),
                ShouldForceResetForPolicy(),
                ShouldForcePrioritiesForPolicy(),
                ShouldForceLimitsForPolicy(),
                ShouldForceEnhanceForPolicy(),
                ref wasEnabled,
                ref savedBasePriority,
                ref savedPriorities1,
                ref savedPriorities2);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Enables or disables the trace log text writer, using the
        /// specified force flags.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trace text writer may be changed.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used to parse any override values.  Optional.
        /// </param>
        /// <param name="policy">
        /// The execution policy that may request forced tracing.
        /// </param>
        /// <param name="enable">
        /// Non-zero to enable the text writer, zero to disable it.
        /// </param>
        /// <param name="forceEnable">
        /// Non-zero to force the text writer to be enabled.
        /// </param>
        /// <param name="forceAutoFile">
        /// Non-zero to force an automatic trace log file to be used.
        /// </param>
        /// <param name="forceAppend">
        /// Non-zero to force the trace log file to be appended to.
        /// </param>
        /// <param name="forceShared">
        /// Non-zero to force the trace log file to be shared.
        /// </param>
        /// <param name="forceTracing">
        /// Non-zero to force the trace priority override to be applied.
        /// </param>
        /// <param name="forceReset">
        /// Non-zero to force the tracing subsystem to be reset.
        /// </param>
        /// <param name="forcePriorities">
        /// Non-zero to force the trace priorities to be adjusted.
        /// </param>
        /// <param name="forceLimits">
        /// Non-zero to force the trace limits to be adjusted.
        /// </param>
        /// <param name="forceEnhance">
        /// Non-zero to force the trace output to be enhanced.
        /// </param>
        /// <param name="wasEnabled">
        /// On input, whether the text writer was previously enabled; upon
        /// return, whether it is now enabled.
        /// </param>
        /// <param name="savedBasePriority">
        /// Upon return, receives the saved base trace priority.
        /// </param>
        /// <param name="savedPriorities1">
        /// Upon return, receives the saved global trace priorities.
        /// </param>
        /// <param name="savedPriorities2">
        /// Upon return, receives the saved interpreter trace priorities.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void MaybeEnableOrDisableTextWriter( /* CORE */
            Interpreter interpreter,              /* in */
            CultureInfo cultureInfo,              /* in: OPTIONAL */
            ExecutionPolicy? policy,              /* in */
            bool enable,                          /* in */
            bool forceEnable,                     /* in: via PolicyTraceFlags */
            bool forceAutoFile,                   /* in: via PolicyTraceFlags */
            bool forceAppend,                     /* in: via PolicyTraceFlags */
            bool forceShared,                     /* in: via PolicyTraceFlags */
            bool forceTracing,                    /* in: via PolicyTraceFlags */
            bool forceReset,                      /* in: via PolicyTraceFlags */
            bool forcePriorities,                 /* in: via PolicyTraceFlags */
            bool forceLimits,                     /* in: via PolicyTraceFlags */
            bool forceEnhance,                    /* in: via PolicyTraceFlags */
            ref bool wasEnabled,                  /* in, out */
            ref TracePriority? savedBasePriority, /* out */
            ref TracePriority? savedPriorities1,  /* out */
            ref TracePriority? savedPriorities2   /* out */
            )
        {
            if (enable)
            {
                wasEnabled = false;

                if (forceEnable || Utility.HasFlags(
                        policy, ExecutionPolicy.EnableTracing, true))
                {
                    string fileName = Configuration.GetVariable(
                        Constants.CertificateTraceFileEnvVarName);

                    if (String.IsNullOrEmpty(fileName))
                    {
                        if (forceAutoFile || Utility.HasFlags(
                                policy, ExecutionPolicy.AutoTraceFile,
                                true))
                        {
                            string envVarName = null;

                            GetFileEnvVarName(ref envVarName);

                            if (envVarName != null)
                            {
                                fileName = Configuration.GetVariable(
                                    envVarName);

                                if (String.IsNullOrEmpty(fileName))
                                {
                                    string directory = Utility.GetTempPath(
                                        interpreter);

                                    //
                                    // NOTE: Generate a unique temporary
                                    //       file name, in the temporary
                                    //       directory, to contain the
                                    //       trace log.  It will have a
                                    //       prefix of "harpy-*" and an
                                    //       extension of ".log".
                                    //
                                    // BUGFIX: Use newly exported from
                                    //         Utility class to obtain
                                    //         the temporary directory,
                                    //         so it can benefit from
                                    //         the environment variable
                                    //         overrides used from the
                                    //         test suite, etc.
                                    //
                                    Result error = null;

                                    fileName = Utility.GetUniquePath(
                                        interpreter, directory,
                                        autoTraceFileNamePrefix,
                                        FileExtension.Log, ref error);

                                    if (fileName != null)
                                    {
                                        //
                                        // NOTE: Save the generated file
                                        //       name for next time, using
                                        //       our private environment
                                        //       variable name.  This is
                                        //       done because we want to
                                        //       make sure all the trace
                                        //       output from within this
                                        //       process ends up in the
                                        //       same file, even when the
                                        //       file name was not manually
                                        //       specified using the public
                                        //       environment variable.
                                        //
                                        Utility.SetEnvironmentVariable(
                                            envVarName, fileName, false);
                                    }
                                    else
                                    {
                                        //
                                        // HACK: Failing to obtain a unique
                                        //       trace file name is a very
                                        //       high priority situation.
                                        //
                                        // BUGFIX: Prior to beta 50, there
                                        //         was a bug in the core
                                        //         library that could cause
                                        //         the GetUniquePath method
                                        //         to return an error.  Now,
                                        //         it should be "impossible"
                                        //         for this trace message to
                                        //         be seen.
                                        //
                                        if (useFallbackLogFileName)
                                        {
                                            GetFallbackLogFileName(
                                                directory, ref fileName);

                                            Utility.SetEnvironmentVariable(
                                                envVarName, fileName, false);
                                        }

                                        /* NO RESULT */
                                        DebugTrace(interpreter, String.Format(
                                            "MaybeEnableOrDisableTextWriter: " +
                                            "no unique path was found, fallback " +
                                            "fileName = {0}, previous error = {1}",
                                            Utility.FormatWrapOrNull(fileName),
                                            Utility.FormatWrapOrNull(error)),
                                            typeof(CertificateTraceOps).Name,
                                            TracePriority.Highest, 0);
                                    }
                                }
                            }
                        }
                    }

                    //
                    // HACK: Make sure the trace log file name is a fully
                    //       qualified path.  If not, it will be put into
                    //       the temporary directory.
                    //
                    MaybeMakeTemporaryFileName(interpreter, ref fileName);

                    //
                    // NOTE: The trace log file must not exist *unless* the
                    //       appropriate execution policy flag is set.  In
                    //       that case, it will be appended to.
                    //
                    bool append = forceAppend || Utility.HasFlags(
                        policy, ExecutionPolicy.AppendTracing, true);

                    if (!String.IsNullOrEmpty(fileName) &&
                        (append || !File.Exists(fileName)))
                    {
                        bool shared = forceShared || Utility.HasFlags(
                            policy, ExecutionPolicy.SharedTracing, true);

                        EnableTextWriter(
                            interpreter, fileName, append, shared,
                            DataOps.GetDefaultEncoding(), false,
                            ref wasEnabled);

                        if (wasEnabled &&
                            (Interlocked.Increment(ref enabledCount) == 1))
                        {
                            //
                            // BUGFIX: Resetting of the tracing subsystem
                            //         should only be done on the first
                            //         call to enable, if at all.
                            //
                            if (forceReset || Utility.HasFlags(
                                    policy, ExecutionPolicy.ResetTracing,
                                    true))
                            {
                                CreateAndProcessClientData(interpreter,
                                    enable);
                            }

                            /* NO RESULT */
                            AdjustPrioritiesAndLimits(
                                interpreter, cultureInfo, policy, enable,
                                forceTracing, forcePriorities, forceLimits,
                                forceEnhance, ref savedBasePriority,
                                ref savedPriorities1, ref savedPriorities2);

                            if (ultraVerbose)
                            {
                                /* NO RESULT */
                                DebugTrace(interpreter, String.Format(
                                    "MaybeEnableOrDisableTextWriter: " +
                                    "WAS JUST ENABLED using file {0}",
                                    Utility.FormatWrapOrNull(fileName)),
                                    typeof(CertificateTraceOps).Name,
                                    TracePriority.Highest, 0);
                            }
                        }
                    }
                }
            }
            else if (wasEnabled &&
                (Interlocked.Decrement(ref enabledCount) == 0))
            {
                //
                // HACK: If the special environment variable is set,
                //       do not disable any of the handling provided
                //       by this class.
                //
                if (Configuration.DoesVariableExist(
                        Constants.HarpyPersistentTracingEnvVarName))
                {
                    return;
                }

                if (ultraVerbose)
                {
                    /* NO RESULT */
                    DebugTrace(interpreter,
                        "MaybeEnableOrDisableTextWriter: " +
                        "ABOUT TO BE DISABLED",
                        typeof(CertificateTraceOps).Name,
                        TracePriority.Highest, 0);
                }

                /* NO RESULT */
                AdjustPrioritiesAndLimits(
                    interpreter, cultureInfo, policy, enable,
                    forceTracing, forcePriorities, forceLimits,
                    forceEnhance, ref savedBasePriority,
                    ref savedPriorities1, ref savedPriorities2);

                //
                // NOTE: Attempt to disable the text writer.  If that fails,
                //       re-increment the enabled count so that subsequent
                //       attempts can be made.
                //
                DisableTextWriter(interpreter, ref wasEnabled);

                if (wasEnabled) /* NOTE: False means success. */
                    Interlocked.Increment(ref enabledCount);

                //
                // HACK: Even though this method is operating in "disable"
                //       mode, reset the core tracing subsystem, if needed.
                //       This is probably not ideal (since it uses the same
                //       flag as "enable" mode); however, it was deemed to
                //       be better than nothing.
                //
                if (ResetTracingOnDisable)
                {
                    if (forceReset || Utility.HasFlags(
                            policy, ExecutionPolicy.ResetTracing, true))
                    {
                        CreateAndProcessClientData(interpreter, enable);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ensures the specified trace log file name is a fully qualified
        /// path, placing relative names in the temporary directory.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to obtain the temporary directory.
        /// </param>
        /// <param name="fileName">
        /// The trace log file name to qualify, in place.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void MaybeMakeTemporaryFileName( /* CORE */
            Interpreter interpreter, /* in */
            ref string fileName      /* in, out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
                return;

            fileName = Utility.ExpandEnvironmentVariables(fileName);

            if (Utility.GetPathType(fileName) != PathType.Relative)
                return;

            string directory = Utility.GetTempPath(interpreter);

            if (String.IsNullOrEmpty(directory))
                return;

            string fileNameOnly = Path.GetFileName(fileName);

            if (String.IsNullOrEmpty(fileNameOnly))
                return;

            fileName = Path.Combine(directory, fileNameOnly);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens the trace log file and installs it as the trace text
        /// writer for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to install the trace text writer on.
        /// </param>
        /// <param name="fileName">
        /// The name of the trace log file to open.
        /// </param>
        /// <param name="append">
        /// Non-zero to append to an existing trace log file.
        /// </param>
        /// <param name="shared">
        /// Non-zero to open the trace log file in shared mode.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use for the trace log file.
        /// </param>
        /// <param name="force">
        /// Non-zero to replace an existing trace text writer.
        /// </param>
        /// <param name="wasEnabled">
        /// Upon return, set to non-zero if the text writer was enabled.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void EnableTextWriter( /* CORE */
            Interpreter interpreter, /* in */
            string fileName,         /* in */
            bool append,             /* in */
            bool shared,             /* in */
            Encoding encoding,       /* in */
            bool force,              /* in */
            ref bool wasEnabled      /* out */
            )
        {
            TextWriter textWriter = null;

            try
            {
                if (interpreter == null)
                    return;

                bool locked = false;

                try
                {
                    interpreter.TryLock(ref locked); /* TRANSACTIONAL */

                    if (locked)
                    {
                        textWriter = interpreter.TraceTextWriter;

                        if (textWriter == null)
                        {
                            goto skipClose;
                        }
                        else if (!force)
                        {
                            textWriter = null; /* DO NOT CLOSE */
                            return;
                        }

                        CloseTextWriter(interpreter, ref textWriter);

                        if (textWriter != null)
                            return;

                        interpreter.TraceTextWriter = null;

                    skipClose:

                        OpenTextWriter(
                            interpreter, fileName, append, shared,
                            encoding, ref textWriter);

                        if (textWriter == null)
                            return;

                        interpreter.TraceTextWriterOwned = false;
                        interpreter.TraceTextWriter = textWriter;

                        textWriter = null; /* DO NOT CLOSE */
                        wasEnabled = true; /* SUCCESS */
                    }
                }
                finally
                {
                    interpreter.ExitLock(ref locked); /* TRANSACTIONAL */
                }
            }
            catch (Exception e)
            {
                //
                // HACK: Failing to enable the tracing subsystem is a
                //       very high priority situation.
                //
                /* NO RESULT */
                DebugTrace(
                    interpreter, e, typeof(CertificateTraceOps).Name,
                    TracePriority.Highest, 0);
            }
            finally
            {
                CloseTextWriter(interpreter, ref textWriter);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the trace text writer for the specified interpreter and
        /// removes it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to remove the trace text writer from.
        /// </param>
        /// <param name="wasEnabled">
        /// On input, whether the text writer was enabled; upon return,
        /// set to zero if it was successfully disabled.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void DisableTextWriter( /* CORE */
            Interpreter interpreter, /* in */
            ref bool wasEnabled      /* out */
            )
        {
            TextWriter textWriter = null;

            try
            {
                if (interpreter == null)
                    return;

                bool locked = false;

                try
                {
                    interpreter.TryLock(ref locked); /* TRANSACTIONAL */

                    if (locked)
                    {
                        textWriter = interpreter.TraceTextWriter;

                        if (textWriter == null)
                            return;

                        CloseTextWriter(interpreter, ref textWriter);

                        if (textWriter != null)
                            return;

                        interpreter.TraceTextWriter = null;
                        interpreter.TraceTextWriterOwned = true;

                        wasEnabled = false; /* SUCCESS */
                    }
                }
                finally
                {
                    interpreter.ExitLock(ref locked); /* TRANSACTIONAL */
                }
            }
            catch (Exception e)
            {
                //
                // HACK: Failing to disable the tracing subsystem is a
                //       very high priority situation.
                //
                /* NO RESULT */
                DebugTrace(
                    interpreter, e, typeof(CertificateTraceOps).Name,
                    TracePriority.Highest, 0);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Opens a trace text writer that mirrors the configuration of
        /// the specified trace stream writer.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the trace text writer.
        /// </param>
        /// <param name="streamWriter">
        /// The trace stream writer whose configuration is mirrored.
        /// </param>
        /// <param name="textWriter">
        /// Upon return, receives the opened trace text writer.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void OpenTextWriter( /* CORE? */
            Interpreter interpreter,        /* in */
            TraceStreamWriter streamWriter, /* in */
            ref TextWriter textWriter       /* in, out */
            )
        {
            if (streamWriter == null)
                return;

            OpenTextWriter(
                interpreter, streamWriter.FileName, streamWriter.Append,
                streamWriter.Shared, streamWriter.Encoding, ref textWriter);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens a trace text writer for the specified trace log file,
        /// optionally validating the file name first.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the trace text writer.
        /// </param>
        /// <param name="fileName">
        /// The name of the trace log file to open.
        /// </param>
        /// <param name="append">
        /// Non-zero to append to an existing trace log file.
        /// </param>
        /// <param name="shared">
        /// Non-zero to open the trace log file in shared mode.
        /// </param>
        /// <param name="encoding">
        /// The encoding to use for the trace log file.
        /// </param>
        /// <param name="textWriter">
        /// Upon return, receives the opened trace text writer.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void OpenTextWriter( /* CORE */
            Interpreter interpreter,  /* in */
            string fileName,          /* in */
            bool append,              /* in */
            bool shared,              /* in */
            Encoding encoding,        /* in */
            ref TextWriter textWriter /* in, out */
            )
        {
            try
            {
                if (textWriter == null)
                {
                    if (shared)
                    {
                        if (failSafeLogFileName)
                        {
                            if (String.IsNullOrEmpty(fileName))
                            {
                                throw new ArgumentException(
                                    "log file path cannot be null or empty");
                            }

                            if (Utility.GetPathType(
                                    fileName) != PathType.Absolute)
                            {
                                throw new ArgumentException(
                                    "log file path must be absolute");
                            }

                            if (Directory.Exists(fileName))
                            {
                                throw new ArgumentException(
                                    "log file path cannot be a directory");
                            }

                            if (!Directory.Exists(
                                    Path.GetDirectoryName(fileName)))
                            {
                                throw new ArgumentException(
                                    "log file path must have directory");
                            }
                        }

                        textWriter = new TraceStreamWriter( /* throw */
                            fileName, append, shared, encoding,
                            new FileStream(fileName, append ?
                            FileMode.Append : FileMode.CreateNew,
                            FileAccess.Write, FileShare.ReadWrite));
                    }
                    else
                    {
                        textWriter = new TraceStreamWriter( /* throw */
                            fileName, append, shared, encoding);
                    }
                }
            }
            catch (Exception e)
            {
                //
                // HACK: Failing to initialize the tracing subsystem is a
                //       very high priority situation.  This could use the
                //       complaint subsystem; however, that was considered
                //       to be overkill.
                //
                /* NO RESULT */
                DebugTrace(
                    interpreter, e, typeof(CertificateTraceOps).Name,
                    TracePriority.Highest, 0);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Closes the specified trace text writer, ignoring any errors
        /// that occur.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the trace text writer.
        /// </param>
        /// <param name="textWriter">
        /// The trace text writer to close, cleared upon success.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void CloseTextWriter( /* CORE */
            Interpreter interpreter,  /* in */
            ref TextWriter textWriter /* in, out */
            )
        {
            try
            {
                if (textWriter != null)
                {
                    textWriter.Close(); /* throw */
                    textWriter = null;
                }
            }
            catch (Exception e)
            {
                //
                // HACK: Failing to terminate tracing to the log file is a
                //       very high priority situation.  This could use the
                //       complaint subsystem; however, that was considered
                //       to be overkill.
                //
                /* NO RESULT */
                DebugTrace(
                    interpreter, e, typeof(CertificateTraceOps).Name,
                    TracePriority.Highest, 0);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends a captured stack trace to the trace message when the
        /// appropriate priority flags or environment variable are set.
        /// </summary>
        /// <param name="message">
        /// The trace message to enhance, in place.
        /// </param>
        /// <param name="priority">
        /// The trace priority to inspect and update, in place.
        /// </param>
        [Conditional("DEBUG_TRACE")]
        private static void MaybeEnhanceWithStackTrace( /* CORE */
            ref string message,        /* in, out */
            ref TracePriority priority /* in, out */
            )
        {
            if (!Utility.HasFlags(
                    priority, TracePriority.User1, true) &&
                (Utility.HasFlags(
                    priority, TracePriority.User0, true) ||
                Configuration.DoesVariableExist(
                    Constants.ForceStackTraceEnvVarName)))
            {
                StringBuilder builder = new StringBuilder();

                if (!String.IsNullOrEmpty(message))
                    builder.AppendLine(message);

                builder.AppendLine();

                builder.AppendFormat(String.Format(
                    StackTraceStartFormat, StackTraceName));

                builder.AppendLine();

                builder.AppendFormat("{0}",
                    new StackTrace(true).ToString().TrimEnd());

                builder.AppendLine();

                builder.AppendFormat(String.Format(
                    StackTraceEndFormat, StackTraceName));

                builder.AppendLine();

                message = builder.ToString();

                priority &= ~TracePriority.EnableStackFlag;
                priority |= TracePriority.User1; /* ONCE */
            }
        }

        ///////////////////////////////////////////////////////////////////////

        #region Public Trace Methods
        /// <summary>
        /// Logs the specified trace message to the active log, if any,
        /// and emits it via the debug tracing subsystem.
        /// </summary>
        /// <param name="message">
        /// The trace message to log and emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        /// <param name="skipFrames">
        /// The number of stack frames to skip when reporting the source.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        // [Conditional("DEBUG_TRACE")]
        public static void MaybeLogAndDebugTrace( /* CORE */
            string message,         /* in */
            string category,        /* in */
            TracePriority priority, /* in */
            int skipFrames          /* in */
            )
        {
            /* NO RESULT */
            MaybeLogAndDebugTrace(
                LogClientData(false), message, category, priority,
                skipFrames + 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logs the specified exception to the active log, if any, and
        /// emits it via the debug tracing subsystem.
        /// </summary>
        /// <param name="exception">
        /// The exception to log and emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        /// <param name="skipFrames">
        /// The number of stack frames to skip when reporting the source.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        // [Conditional("DEBUG_TRACE")]
        public static void MaybeLogAndDebugTrace( /* CORE */
            Exception exception,    /* in */
            string category,        /* in */
            TracePriority priority, /* in */
            int skipFrames          /* in */
            )
        {
            /* NO RESULT */
            MaybeLogAndDebugTrace(
                LogClientData(false), exception, category, priority,
                skipFrames + 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logs the specified trace message to the given log client data,
        /// if any, and emits it via the debug tracing subsystem.
        /// </summary>
        /// <param name="clientData">
        /// The log client data to append to.  Optional.
        /// </param>
        /// <param name="message">
        /// The trace message to log and emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        /// <param name="skipFrames">
        /// The number of stack frames to skip when reporting the source.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        // [Conditional("DEBUG_TRACE")]
        public static void MaybeLogAndDebugTrace( /* CORE */
            ILogClientData clientData, /* in: OPTIONAL */
            string message,            /* in */
            string category,           /* in */
            TracePriority priority,    /* in */
            int skipFrames             /* in */
            )
        {
            /* NO RESULT */
            MaybeEnhanceWithStackTrace(
                ref message, ref priority);

            /* IGNORED */
            MaybeLog(clientData, message, priority);

            /* NO RESULT */
            DebugTrace(
                message, category, priority, skipFrames + 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logs the specified exception to the given log client data, if
        /// any, and emits it via the debug tracing subsystem.
        /// </summary>
        /// <param name="clientData">
        /// The log client data to append to.  Optional.
        /// </param>
        /// <param name="exception">
        /// The exception to log and emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        /// <param name="skipFrames">
        /// The number of stack frames to skip when reporting the source.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        // [Conditional("DEBUG_TRACE")]
        public static void MaybeLogAndDebugTrace( /* CORE */
            ILogClientData clientData, /* in: OPTIONAL */
            Exception exception,       /* in */
            string category,           /* in */
            TracePriority priority,    /* in */
            int skipFrames             /* in */
            )
        {
            /* IGNORED */
            MaybeLog(clientData, exception, priority);

            /* NO RESULT */
            DebugTrace(
                exception, category, priority, skipFrames + 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits the specified trace message via the debug tracing
        /// subsystem.
        /// </summary>
        /// <param name="message">
        /// The trace message to emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        public static void DebugTrace( /* CORE */
            string message,        /* in */
            string category,       /* in */
            TracePriority priority /* in */
            )
        {
            /* NO RESULT */
            DebugTrace(message, category, priority, 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits the specified exception via the debug tracing subsystem.
        /// </summary>
        /// <param name="exception">
        /// The exception to emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        public static void DebugTrace( /* CORE */
            Exception exception,   /* in */
            string category,       /* in */
            TracePriority priority /* in */
            )
        {
            /* NO RESULT */
            DebugTrace(exception, category, priority, 1);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Trace Methods
        /// <summary>
        /// Emits the specified trace message via the debug tracing
        /// subsystem, applying the trace priority overrides.
        /// </summary>
        /// <param name="message">
        /// The trace message to emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        /// <param name="skipFrames">
        /// The number of stack frames to skip when reporting the source.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        private static void DebugTrace( /* CORE */
            string message,         /* in */
            string category,        /* in */
            TracePriority priority, /* in */
            int skipFrames          /* in */
            )
        {
            TracePriority savedPriority = TracePriority.None;

            /* NO RESULT */
            MaybeAdjustPriority(
                true, ref priority, ref savedPriority);

            try
            {
                Interpreter interpreter = Interpreter.GetActive();

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                /* NO RESULT */
                MaybeEnhanceWithTroubleshooting(interpreter,
                    Constants.VerboseTracePriorityEnvVarName,
                    null, false, true, ref priority);
#endif

                priority |= TracePriority.ViaWrapperFromPlugin;

                /* NO RESULT */
                MaybeEnhanceWithStackTrace(
                    ref message, ref priority);

                /* NO RESULT */
                Utility.DebugTrace(
                    interpreter, message, category,
                    priority, skipFrames + 1);
            }
            finally
            {
                /* NO RESULT */
                MaybeAdjustPriority(
                    false, ref priority, ref savedPriority);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits the specified exception via the debug tracing subsystem,
        /// applying the trace priority overrides.
        /// </summary>
        /// <param name="exception">
        /// The exception to emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        /// <param name="skipFrames">
        /// The number of stack frames to skip when reporting the source.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        private static void DebugTrace( /* CORE */
            Exception exception,    /* in */
            string category,        /* in */
            TracePriority priority, /* in */
            int skipFrames          /* in */
            )
        {
            TracePriority savedPriority = TracePriority.None;

            /* NO RESULT */
            MaybeAdjustPriority(
                true, ref priority, ref savedPriority);

            try
            {
                Interpreter interpreter = Interpreter.GetActive();

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                /* NO RESULT */
                MaybeEnhanceWithTroubleshooting(interpreter,
                    Constants.VerboseTracePriorityEnvVarName,
                    null, false, true, ref priority);
#endif

                priority |= TracePriority.ViaWrapperFromPlugin;

                /* NO RESULT */
                Utility.DebugTrace(
                    interpreter, exception, category,
                    priority, skipFrames + 1);
            }
            finally
            {
                /* NO RESULT */
                MaybeAdjustPriority(
                    false, ref priority, ref savedPriority);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits the specified trace message via the debug tracing
        /// subsystem for the given interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the trace message.
        /// </param>
        /// <param name="message">
        /// The trace message to emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        /// <param name="skipFrames">
        /// The number of stack frames to skip when reporting the source.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        private static void DebugTrace( /* CORE */
            Interpreter interpreter, /* in */
            string message,          /* in */
            string category,         /* in */
            TracePriority priority,  /* in */
            int skipFrames           /* in */
            )
        {
            TracePriority savedPriority = TracePriority.None;

            /* NO RESULT */
            MaybeAdjustPriority(
                true, ref priority, ref savedPriority);

            try
            {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                /* NO RESULT */
                MaybeEnhanceWithTroubleshooting(interpreter,
                    Constants.VerboseTracePriorityEnvVarName,
                    null, false, true, ref priority);
#endif

                priority |= TracePriority.ViaWrapperFromPlugin;

                /* NO RESULT */
                MaybeEnhanceWithStackTrace(
                    ref message, ref priority);

                /* NO RESULT */
                Utility.DebugTrace(
                    interpreter, message, category,
                    priority, skipFrames + 1);
            }
            finally
            {
                /* NO RESULT */
                MaybeAdjustPriority(
                    false, ref priority, ref savedPriority);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Emits the specified exception via the debug tracing subsystem
        /// for the given interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the exception.
        /// </param>
        /// <param name="exception">
        /// The exception to emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        /// <param name="skipFrames">
        /// The number of stack frames to skip when reporting the source.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        private static void DebugTrace( /* CORE */
            Interpreter interpreter, /* in */
            Exception exception,     /* in */
            string category,         /* in */
            TracePriority priority,  /* in */
            int skipFrames           /* in */
            )
        {
            TracePriority savedPriority = TracePriority.None;

            /* NO RESULT */
            MaybeAdjustPriority(
                true, ref priority, ref savedPriority);

            try
            {
#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                /* NO RESULT */
                MaybeEnhanceWithTroubleshooting(interpreter,
                    Constants.VerboseTracePriorityEnvVarName,
                    null, false, true, ref priority);
#endif

                priority |= TracePriority.ViaWrapperFromPlugin;

                /* NO RESULT */
                Utility.DebugTrace(
                    interpreter, exception, category,
                    priority, skipFrames + 1);
            }
            finally
            {
                /* NO RESULT */
                MaybeAdjustPriority(
                    false, ref priority, ref savedPriority);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logs and emits the specified network-related trace message
        /// when network tracing is enabled.
        /// </summary>
        /// <param name="message">
        /// The trace message to log and emit.
        /// </param>
        /// <param name="category">
        /// The trace category for the message.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the message.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG_TRACE")]
        public static void NetworkDebugTrace( /* CORE */
            string message,        /* in */
            string category,       /* in */
            TracePriority priority /* in */
            )
        {
            if (Configuration.DoesVariableExist(
                    Constants.NetworkTraceEnvVarName))
            {
                /* TRANSIENT */
                priority |= TracePriority.User0;

                ILogClientData logClientData = LogClientData(false);

                if ((logClientData == null) &&
                    Configuration.DoesVariableExist(
                        Constants.ForceLogNetworkEnvVarName))
                {
                    logClientData = LogClientData(true);
                }

                /* NO RESULT */
                MaybeLogAndDebugTrace(
                    logClientData, message, category, priority, 1);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Log Methods
        /// <summary>
        /// Determines whether the specified log client data is non-null
        /// and has not been disposed.
        /// </summary>
        /// <param name="logClientData">
        /// The log client data to check.
        /// </param>
        /// <returns>
        /// Non-zero if the log client data can be used.
        /// </returns>
        private static bool CanUseLogClientData( /* CORE */
            ILogClientData logClientData /* in */
            )
        {
            if (logClientData == null)
                return false;

            if (logClientData.Disposed)
                return false;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Obtains the log client data associated with the active
        /// interpreter, optionally creating it when not available.
        /// </summary>
        /// <param name="create">
        /// Non-zero to create new log client data when none is found.
        /// </param>
        /// <returns>
        /// The log client data, or null if none is available and creation
        /// was not requested.
        /// </returns>
        public static ILogClientData LogClientData( /* CORE */
            bool create /* in */
            )
        {
            //
            // HACK: Since ILogClientData instance is not always
            //       available from deeper within the call stack,
            //       attempt to peek into the active interpreter
            //       (pair) in order to obtain it.
            //
            InterpreterPair anyPair = Utility.PeekActiveInterpreter();

            if (anyPair == null)
                goto maybeCreate;

            ILogClientData logClientData = anyPair.Y as ILogClientData;

            if (CanUseLogClientData(logClientData))
                return logClientData;

            IBaseClientData baseClientData = anyPair.Y as IBaseClientData;

            if (baseClientData != null)
            {
                logClientData = baseClientData.Log as ILogClientData;

                if (CanUseLogClientData(logClientData))
                    return logClientData;
            }

        maybeCreate:

            if (create)
            {
                MutableInterpreterPair mutableAnyPair =
                    anyPair as MutableInterpreterPair;

                Interpreter interpreter; /* REUSED */

                if ((mutableAnyPair != null) &&
                    (mutableAnyPair.Y == null))
                {
                    interpreter = mutableAnyPair.X;

                    if (interpreter == null)
                        interpreter = Interpreter.GetAny();

                    mutableAnyPair.Y = new ScriptLogClientData(
                        interpreter, SharedOps.GetPlugin(interpreter),
                        null, PolicyType.Trace, null);

                    return mutableAnyPair.Y as ILogClientData;
                }
                else
                {
                    interpreter = Interpreter.GetAny();

                    return new ScriptLogClientData(
                        interpreter, SharedOps.GetPlugin(interpreter),
                        null, PolicyType.Trace, null);
                }
            }

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats a single trace log entry from the specified priority,
        /// timestamp, and value.
        /// </summary>
        /// <param name="policyType">
        /// The policy type associated with the entry.  Optional.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the entry.
        /// </param>
        /// <param name="now">
        /// The timestamp for the entry.
        /// </param>
        /// <param name="object">
        /// The value to include in the entry.  Optional.
        /// </param>
        /// <returns>
        /// The formatted log entry.
        /// </returns>
        private static string FormatLogEntry( /* CORE */
            PolicyType? policyType, /* in: OPTIONAL */
            TracePriority priority, /* in */
            DateTime now,           /* in */
            object @object          /* in: OPTIONAL */
            )
        {
            return String.Format("{0} {1}[{2}] {3}",
                Utility.FormatTracePriority(
                    priority, basePriorityOnly, true),
                (policyType != null) ? String.Format(
                    "({0}) ", policyType) : null,
                DataOps.FormatTimeStamp(now),
                DataOps.MaybeNullOrEmpty(@object));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the specified value to the trace log managed by the
        /// given log client data, if any.
        /// </summary>
        /// <param name="clientData">
        /// The log client data to append to.  Optional.
        /// </param>
        /// <param name="object">
        /// The value to append to the log.  Optional.
        /// </param>
        /// <param name="priority">
        /// The trace priority for the entry.
        /// </param>
        /// <returns>
        /// The result of the append operation, or null if no log client
        /// data was provided.
        /// </returns>
        private static bool? MaybeLog( /* CORE */
            ILogClientData clientData, /* in: OPTIONAL */
            object @object,            /* in: OPTIONAL */
            TracePriority priority     /* in */
            )
        {
            //
            // NOTE: If the caller provided us with a trace log,
            //       attempt to use it now, by appending to it.
            //       After that, also call into the debug trace
            //       subsystem.
            //
            if (clientData == null)
                return null;

            TracePriority savedPriority = TracePriority.None;

            /* NO RESULT */
            MaybeAdjustPriority(
                true, ref priority, ref savedPriority);

            try
            {
                Interpreter interpreter = clientData.Interpreter;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
                /* NO RESULT */
                MaybeEnhanceWithTroubleshooting(interpreter,
                    Constants.VerboseTracePriorityEnvVarName,
                    null, false, true, ref priority);
#endif

                priority |= TracePriority.ViaWrapperFromPlugin;

                DateTime now = Utility.GetUtcNow();

                string formatted = FormatLogEntry(
                    clientData.PolicyType, priority, now,
                    @object);

                return clientData.AppendToFile(formatted);
            }
            finally
            {
                /* NO RESULT */
                MaybeAdjustPriority(
                    false, ref priority, ref savedPriority);
            }
        }
        #endregion
    }
}
