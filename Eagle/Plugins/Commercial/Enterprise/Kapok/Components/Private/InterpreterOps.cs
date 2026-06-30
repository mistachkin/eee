/*
 * InterpreterOps.cs --
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
using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Kapok.Components.Public;
using Kapok.Interfaces.Public;

using PhaseList = System.Collections.Generic.List<
    Kapok.Components.Private.InterpreterPhase>;

using PhasePair = System.Collections.Generic.KeyValuePair<
    Kapok.Components.Private.InterpreterPhase,
    Eagle._Components.Public.Interpreter>;

using ThreadPair = System.Collections.Generic.KeyValuePair<
    long, System.Collections.Generic.Dictionary<
    Kapok.Components.Private.InterpreterPhase,
    Eagle._Components.Public.Interpreter>>;

using PhaseDictionary = System.Collections.Generic.Dictionary<
    Kapok.Components.Private.InterpreterPhase,
    Eagle._Components.Public.Interpreter>;

using ThreadDictionary = System.Collections.Generic.Dictionary<
    long, System.Collections.Generic.Dictionary<
    Kapok.Components.Private.InterpreterPhase,
    Eagle._Components.Public.Interpreter>>;

using CleanupPair = Eagle._Components.Public.AnyPair<
    long, Kapok.Components.Public.SecurityFlags>;

using ArgsList = System.Collections.Generic.IEnumerable<string>;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Manages a per-thread cache of interpreters keyed by interpreter phase,
    /// creating, validating, refreshing (on staleness), and disposing them,
    /// and tracking get/create/dispose statistics.
    /// </summary>
    [ObjectId("dc03ba7d-d78e-454e-9968-14eabe988251")]
    internal static class InterpreterOps
    {
        #region Private Constants
        /// <summary>
        /// The text used to display a null interpreter.
        /// </summary>
        private const string DisplayNull = "<null>";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the interpreter cache.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The thread-local cache of interpreters, keyed by phase.
        /// </summary>
        private static readonly ThreadDictionary interpreters =
            new ThreadDictionary();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The number of successful cache retrievals.
        /// </summary>
        private static long getWasOkCount = 0;
        /// <summary>
        /// The number of cache retrievals that found an unusable interpreter.
        /// </summary>
        private static long getUnusableCount = 0;
        /// <summary>
        /// The number of successful interpreter creations.
        /// </summary>
        private static long createWasOkCount = 0;
        /// <summary>
        /// The number of failed interpreter creations.
        /// </summary>
        private static long createWasNotOkCount = 0;
        /// <summary>
        /// The number of successful interpreter disposals.
        /// </summary>
        private static long disposeWasOkCount = 0;
        /// <summary>
        /// The number of disposals skipped because the interpreter could not
        /// be made active.
        /// </summary>
        private static long disposeCannotActiveCount = 0;
        /// <summary>
        /// The number of disposals skipped because disposal could not be
        /// enabled.
        /// </summary>
        private static long disposeCannotEnableCount = 0;
        /// <summary>
        /// The number of failed interpreter disposals.
        /// </summary>
        private static long disposeWasNotOkCount = 0;
        /// <summary>
        /// The number of disposals skipped because the interpreter was already
        /// disposed.
        /// </summary>
        private static long disposeAlreadyCount = 0;
        /// <summary>
        /// The number of disposals skipped because the interpreter was
        /// invalid.
        /// </summary>
        private static long disposeInvalidCount = 0;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Non-zero when a stale-interpreter cleanup is pending.
        /// </summary>
        private static int cleanupPending = 0;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Support Methods
        /// <summary>
        /// Gets the default interpreter settings.
        /// </summary>
        /// <returns>
        /// The default settings.
        /// </returns>
        private static IInterpreterSettings DefaultSettings()
        {
            return InterpreterSettings.CreateDefault();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a fresh set of interpreter settings.
        /// </summary>
        /// <returns>
        /// The new settings.
        /// </returns>
        private static IInterpreterSettings CreateSettings()
        {
            IInterpreterSettings interpreterSettings = DefaultSettings();

            if (interpreterSettings != null)
            {
                PluginFlags enablePluginFlags = PluginFlags.LoadOnAnyThread;

#if ISOLATED_PLUGINS
                //
                // HACK: Use of the plugin loader "preview" feature may cause
                //       application domain errors because the base directory
                //       will not be correct within created ASP.NET contexts.
                //       Since the plugin loader feature is (mostly?) useless
                //       for servers anyhow, just disable it.
                //
                // HACK: Actually, this should no longer be needed because the
                //       plugin loader itself has been modified to prevent the
                //       preview feature from being used when the application
                //       domain base directory does not match up with the core
                //       library assembly.
                //
                // enablePluginFlags |= PluginFlags.NoPreview;
#endif

                interpreterSettings.PluginFlags |= enablePluginFlags;
            }

            return interpreterSettings;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new interpreter using the default settings.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments for the interpreter.
        /// </param>
        /// <param name="result">
        /// On failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The created interpreter, or null on failure.
        /// </returns>
        private static Interpreter Create(
            ArgsList args,    /* in */
            ref Result result /* out */
            )
        {
            return Create(CreateSettings(), args, ref result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new interpreter using the specified settings.
        /// </summary>
        /// <param name="interpreterSettings">
        /// The settings used to create the interpreter.
        /// </param>
        /// <param name="args">
        /// The command-line arguments for the interpreter.
        /// </param>
        /// <param name="result">
        /// On failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The created interpreter, or null on failure.
        /// </returns>
        private static Interpreter Create(
            IInterpreterSettings interpreterSettings, /* in */
            ArgsList args,                            /* in */
            ref Result result                         /* out */
            )
        {
            AddExitedEventHandler();

            Interpreter interpreter = Interpreter.Create(
                interpreterSettings, false, ref result);

            if (interpreter != null)
            {
                if (ArgsOps.ShouldUseAutomatic(args))
                    args = ArgsOps.GetAutomatic(interpreter);

                if (args != null)
                {
#if DEBUG || FORCE_TRACE
                    TracePriority priority = TracePriority.MediumLow;
#endif

                    ExitCode exitCode = Interpreter.ShellMainCore(
                        interpreter, args, false, false, ref result);

                    if (exitCode != ExitCode.Success)
                    {
#if DEBUG || FORCE_TRACE
                        //
                        // TODO: There may be some cases where this value
                        //       really should reflect a higher priority.
                        //
                        priority = TracePriority.MediumHigh;
#endif

                        /* NO RESULT */
                        Dispose(ref interpreter);
                    }

#if DEBUG || FORCE_TRACE
                    Utility.DebugTrace(String.Format(
                        "Create(ShellMainCore): interpreter = {0}, " +
                        "args = {1}, exitCode = {2}, result = {3}",
                        Utility.FormatWrapOrNull(
                            (interpreter != null) ?
                                (long?)interpreter.IdNoThrow : null),
                        Utility.FormatWrapOrNull(args), exitCode,
                        Utility.FormatWrapOrNull(result)),
                        typeof(InterpreterOps).Name,
                        priority | TracePriority.FromPlugin);
#endif
                }
            }

            return interpreter; /* MAY BE NULL */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied interpreter is usable.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in question.
        /// </param>
        /// <returns>
        /// Non-zero when the interpreter is usable; otherwise, zero.
        /// </returns>
        private static bool CanUse(
            Interpreter interpreter /* in */
            )
        {
            if (interpreter == null) // NOTE: Do not log, noisy.
                return false;

            if (interpreter.Disposed) /* IMPOSSIBLE (?) */
            {
#if DEBUG || FORCE_TRACE
                Utility.DebugTrace(String.Format(
                    "CanUse: interpreter {0} unusable, it is DISPOSED",
                    Utility.FormatWrapOrNull(interpreter.IdNoThrow)),
                    typeof(InterpreterOps).Name, TracePriority.Highest |
                    TracePriority.FromPlugin);
#endif

                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied interpreter is usable for the given
        /// page.
        /// </summary>
        /// <param name="pageData">
        /// The page data describing the requirements.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter in question.
        /// </param>
        /// <returns>
        /// Non-zero when the interpreter is usable; otherwise, zero.
        /// </returns>
        private static bool CanUse(
            IScriptPageData pageData, /* in */
            Interpreter interpreter   /* in */
            )
        {
            //
            // NOTE: From the perspective of this method, if there
            //       is no page data available -OR- it has caching
            //       disabled, there is simply no cache available;
            //       therefore, return false to indicate that the
            //       caller cannot rely on the cached interpreter,
            //       i.e. the interpreter parameter, which will be
            //       presumed to have come from the cache.
            //
            if ((pageData == null) || !pageData.CacheInterpreter)
                return false;

            return CanUse(interpreter);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied interpreter is stale (older than
        /// the given number of seconds).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in question.
        /// </param>
        /// <param name="maximumSeconds">
        /// The maximum age, in seconds, before the interpreter is considered
        /// stale.
        /// </param>
        /// <returns>
        /// Non-zero when the interpreter is stale; otherwise, zero.
        /// </returns>
        private static bool IsStale(
            Interpreter interpreter, /* in */
            long? maximumSeconds     /* in */
            )
        {
            //
            // NOTE: If there is no interpreter, it cannot be stale.
            //       If there is no maximum idle seconds, nothing is
            //       stale.
            //
            if ((interpreter == null) || (maximumSeconds == null))
                return false;

            return !interpreter.CheckLastAccessed((long)maximumSeconds);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Allows the supplied interpreter to be disposed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in question.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool AllowDispose(
            Interpreter interpreter /* in */
            )
        {
            return AllowOrForbidDispose(interpreter, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Forbids the supplied interpreter from being disposed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in question.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool ForbidDispose(
            Interpreter interpreter /* in */
            )
        {
            return AllowOrForbidDispose(interpreter, false);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Allows or forbids the supplied interpreter from being disposed.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in question.
        /// </param>
        /// <param name="enabled">
        /// Non-zero to allow disposal; zero to forbid it.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool AllowOrForbidDispose(
            Interpreter interpreter, /* in */
            bool enabled             /* in */
            )
        {
            if (interpreter == null)
                return false;

            bool? result = interpreter.SetDisposalEnabled(false, enabled);

            if (result == null)
                return false;

            return (bool)result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes the supplied interpreter, updating the disposal
        /// statistics.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in question.
        /// </param>
        private static void Dispose(
            ref Interpreter interpreter /* in, out */
            )
        {
            if (interpreter != null)
            {
                long id = interpreter.IdNoThrow;

                if (!interpreter.Disposed)
                {

#if DEBUG || FORCE_TRACE
                    Utility.DebugTrace(String.Format(
                        "Dispose({0}): CALLED {1}", id, new StackTrace(true)),
                        typeof(InterpreterOps).Name, TracePriority.Medium |
                        TracePriority.FromPlugin);
#endif

                    if (interpreter.ActiveCount > 0)
                    {
                        Interlocked.Increment(ref disposeCannotActiveCount);

#if DEBUG || FORCE_TRACE
                        Utility.DebugTrace(String.Format(
                            "ActiveCount({0}): FAILED {1}", id,
                            new StackTrace(true)), typeof(InterpreterOps).Name,
                            TracePriority.Highest | TracePriority.FromPlugin);
#endif

                        goto done;
                    }

                    if (!AllowDispose(interpreter))
                    {
                        Interlocked.Increment(ref disposeCannotEnableCount);

#if DEBUG || FORCE_TRACE
                        Utility.DebugTrace(String.Format(
                            "AllowDispose({0}): FAILED {1}", id,
                            new StackTrace(true)), typeof(InterpreterOps).Name,
                            TracePriority.Highest | TracePriority.FromPlugin);
#endif

                        goto done;
                    }

                    if (Utility.TryDisposeObjectOrComplain<Interpreter>(
                            interpreter, ref interpreter) != ReturnCode.Ok)
                    {
                        Interlocked.Increment(ref disposeWasNotOkCount);

#if DEBUG || FORCE_TRACE
                        Utility.DebugTrace(String.Format(
                            "TryDisposeObjectOrComplain({0}): FAILED {1}", id,
                            new StackTrace(true)), typeof(InterpreterOps).Name,
                            TracePriority.Highest | TracePriority.FromPlugin);
#endif

                        goto done;
                    }

                    Interlocked.Increment(ref disposeWasOkCount);
                }
                else
                {
                    Interlocked.Increment(ref disposeAlreadyCount);

#if DEBUG || FORCE_TRACE
                    Utility.DebugTrace(String.Format(
                        "AlreadyDisposed({0}): SKIPPED {1}", id,
                        new StackTrace(true)), typeof(InterpreterOps).Name,
                        TracePriority.Highest | TracePriority.FromPlugin);
#endif
                }

            done:

                interpreter = null;
            }
            else
            {
                Interlocked.Increment(ref disposeInvalidCount);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Cache Management Methods
        //
        // NOTE: The algorithm used by this method is basically as follows:
        //
        //       1. If the caller just created an interpreter, always trace
        //          status.
        //
        //       2. If there is an interpreter for this thread in the cache,
        //          for any phase, created within the last X seconds, always
        //          trace status.
        //
        //       3. Otherwise, do not trace status.
        //
        //       This algorithm is subject to change at any time.  Please do
        //       not rely on the current algorithm.
        //
        /// <summary>
        /// Determines whether the cache status should be traced for the given
        /// parameters.
        /// </summary>
        /// <param name="maximumSeconds">
        /// The staleness threshold, in seconds.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <param name="created">
        /// Non-zero when an interpreter was just created.
        /// </param>
        /// <returns>
        /// Non-zero when the status should be traced; otherwise, zero.
        /// </returns>
        public static bool ShouldTraceCacheStatus(
            long maximumSeconds, /* in */
            bool useThreadId,    /* in */
            bool created         /* in */
            )
        {
            if (created)
                return true; // NOTE: Just created?  Trace it.

            DateTime now = Utility.GetUtcNow();

            foreach (InterpreterPhase phase in
                new InterpreterPhase[] {
                    InterpreterPhase.Validate,
                    InterpreterPhase.Configuration,
                    InterpreterPhase.Server
                })
            {
                Interpreter interpreter = GetCached(
                    phase, useThreadId, true);

                if (interpreter == null)
                    continue;

                DateTime then = interpreter.CreatedNoThrow;

                if (then > now)
                    return true; // NOTE: Time travel?  Interesting.

                if (Convert.ToInt64(now.Subtract(
                        then).TotalSeconds) <= maximumSeconds)
                {
                    return true; // NOTE: Quite fresh?  Trace it.
                }
            }

            return false; // NOTE: Not really interesting.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a list describing the current interpreter cache statistics.
        /// </summary>
        /// <returns>
        /// A list of cache status values.
        /// </returns>
        public static IStringList GetCacheStatus()
        {
            StringPairList result = new StringPairList();

            result.Add("cacheThreadId", GetCacheThreadId(true).ToString());

            result.Add("getWasOkCount", Interlocked.CompareExchange(
                ref getWasOkCount, 0, 0).ToString());

            result.Add("getUnusableCount", Interlocked.CompareExchange(
                ref getUnusableCount, 0, 0).ToString());

            result.Add("createWasOkCount", Interlocked.CompareExchange(
                ref createWasOkCount, 0, 0).ToString());

            result.Add("createWasNotOkCount", Interlocked.CompareExchange(
                ref createWasNotOkCount, 0, 0).ToString());

            result.Add("disposeWasOkCount", Interlocked.CompareExchange(
                ref disposeWasOkCount, 0, 0).ToString());

            result.Add("disposeWasNotOkCount", Interlocked.CompareExchange(
                ref disposeWasNotOkCount, 0, 0).ToString());

            result.Add("disposeCannotActiveCount", Interlocked.CompareExchange(
                ref disposeCannotActiveCount, 0, 0).ToString());

            result.Add("disposeCannotEnableCount", Interlocked.CompareExchange(
                ref disposeCannotEnableCount, 0, 0).ToString());

            result.Add("disposeAlreadyCount", Interlocked.CompareExchange(
                ref disposeAlreadyCount, 0, 0).ToString());

            result.Add("disposeInvalidCount", Interlocked.CompareExchange(
                ref disposeInvalidCount, 0, 0).ToString());

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (interpreters == null)
                {
                    result.Add("interpreters", DisplayNull);
                    goto done;
                }

                foreach (ThreadPair outerPair in interpreters)
                {
                    PhaseDictionary localInterpreters = outerPair.Value;

                    if (localInterpreters == null)
                    {
                        result.Add(outerPair.Key.ToString(), DisplayNull);
                        continue;
                    }

                    result.Add(outerPair.Key.ToString(),
                        localInterpreters.Count.ToString());

                    foreach (PhasePair innerPair in localInterpreters)
                    {
                        result.Add(innerPair.Key.ToString(),
                            Utility.FormatWrapOrNull(innerPair.Value));
                    }
                }
            }

        done:

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the cache key thread id for the operation.
        /// </summary>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <returns>
        /// The thread id, or a shared value when not thread-scoped.
        /// </returns>
        private static long GetCacheThreadId(
            bool useThreadId /* in */
            )
        {
            return useThreadId ? Utility.GetCurrentThreadId() : 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether a usable interpreter is cached for the page and
        /// phase.
        /// </summary>
        /// <param name="pageData">
        /// The page data describing the requirements.
        /// </param>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <returns>
        /// Non-zero when a usable interpreter is cached; otherwise, zero.
        /// </returns>
        private static bool HaveCached(
            IScriptPageData pageData, /* in */
            InterpreterPhase phase,   /* in */
            bool useThreadId          /* in */
            )
        {
            return CanUse(pageData, GetCached(phase, useThreadId, true));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the cached interpreter for the phase, if any.
        /// </summary>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <param name="noAccessed">
        /// Non-zero to not update the last-accessed time.
        /// </param>
        /// <returns>
        /// The cached interpreter, or null when none.
        /// </returns>
        public static Interpreter GetCached(
            InterpreterPhase phase, /* in */
            bool useThreadId,       /* in */
            bool noAccessed         /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (interpreters == null)
                    return null;

                long threadId = GetCacheThreadId(useThreadId);
                PhaseDictionary localInterpreters;

                if (!interpreters.TryGetValue(
                        threadId, out localInterpreters) ||
                    (localInterpreters == null))
                {
                    return null;
                }

                Interpreter oldInterpreter;

                if (!localInterpreters.TryGetValue(
                        phase, out oldInterpreter))
                {
                    return null;
                }

                if (!CanUse(oldInterpreter))
                {
                    /* NO RESULT */
                    Dispose(ref oldInterpreter); /* REDUNDANT */

                    /* IGNORED */
                    localInterpreters.Remove(phase);

                    return null;
                }

                //
                // NOTE: Prevent interpreter from being disposed
                //       by updating its last accessed time.
                //
                if (!noAccessed && (oldInterpreter != null)) /* REDUNDANT */
                    oldInterpreter.UpdateLastAccessed();

                return oldInterpreter;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the cached interpreter for the phase.
        /// </summary>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="newInterpreter">
        /// The interpreter to cache.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <param name="noAccessed">
        /// Non-zero to not update the last-accessed time.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool SetCached(
            InterpreterPhase phase,     /* in */
            Interpreter newInterpreter, /* in */
            bool useThreadId,           /* in */
            bool noAccessed             /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (interpreters == null)
                    return false;

                long threadId = GetCacheThreadId(useThreadId);
                PhaseDictionary localInterpreters;

                if (!interpreters.TryGetValue(
                        threadId, out localInterpreters) ||
                    (localInterpreters == null))
                {
                    localInterpreters = new PhaseDictionary();
                    interpreters[threadId] = localInterpreters;
                }

                Interpreter oldInterpreter;

                if (localInterpreters.TryGetValue(
                        phase, out oldInterpreter))
                {
                    /* NO RESULT */
                    Dispose(ref oldInterpreter); /* REDUNDANT (?) */

                    /* IGNORED */
                    localInterpreters.Remove(phase);
                }

                //
                // HACK: This will (also) detect if the "old" and
                //       "new" interpreters are actually the same,
                //       i.e. due to the "old" interpreter being
                //       disposed (above), thus making the CanUse
                //       check on the "new" interpreter fail here.
                //
                if (!CanUse(newInterpreter))
                    return false;

                //
                // NOTE: Prevent interpreter from being disposed
                //       by updating its last accessed time.
                //
                if (!noAccessed && (newInterpreter != null)) /* REDUNDANT */
                    newInterpreter.UpdateLastAccessed();

                /* NO RESULT */
                localInterpreters[phase] = newInterpreter;

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or creates an interpreter for the phase, optionally using and
        /// refreshing the cache.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments for the interpreter.
        /// </param>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="useCache">
        /// Non-zero to use the cache.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <param name="refresh">
        /// Non-zero to force creation of a fresh interpreter.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The interpreter, or null on failure.
        /// </returns>
        public static Interpreter GetOrCreate(
            ArgsList args,          /* in */
            InterpreterPhase phase, /* in */
            bool useCache,          /* in */
            bool useThreadId,       /* in */
            bool? refresh,          /* in */
            ref Result error        /* out */
            )
        {
            bool created;

            return GetOrCreate(
                args, phase, useCache, useThreadId, refresh,
                out created, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or creates an interpreter for the phase, reporting whether a
        /// new one was created.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments for the interpreter.
        /// </param>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="useCache">
        /// Non-zero to use the cache.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <param name="refresh">
        /// Non-zero to force creation of a fresh interpreter.
        /// </param>
        /// <param name="created">
        /// On output, non-zero when a new interpreter was created.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The interpreter, or null on failure.
        /// </returns>
        public static Interpreter GetOrCreate(
            ArgsList args,          /* in */
            InterpreterPhase phase, /* in */
            bool useCache,          /* in */
            bool useThreadId,       /* in */
            bool? refresh,          /* in */
            out bool created,       /* out */
            ref Result error        /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                created = false;

                if (useCache &&
                    ((refresh == null) || !(bool)refresh))
                {
                    Interpreter oldInterpreter = GetCached(
                        phase, useThreadId, false);

                    if (CanUse(oldInterpreter)) /* REDUNDANT (?) */
                    {
                        Interlocked.Increment(ref getWasOkCount);
                        return oldInterpreter;
                    }
                    else
                    {
                        //
                        // NOTE: The "getUnusableCount" static field
                        //       is used for two distinct issues:
                        //
                        //       1. The interpreter is null.
                        //       2. The interpreter is disposed.
                        //
                        //       There could be two count fields;
                        //       however, that is not needed.
                        //
                        Interlocked.Increment(ref getUnusableCount);
                    }
                }

                //
                // NOTE: If we get to this point, cache has failed
                //       failed to find an interpreter that matches
                //       the specified criteria, e.g. one that may
                //       be used by this thread.
                //
                Interpreter newInterpreter;
                Result result = null;

                newInterpreter = Create(args, ref result);

                if (newInterpreter != null)
                {
                    Interlocked.Increment(ref createWasOkCount);
                    created = true;
                }
                else
                {
                    //
                    // NOTE: This should be quite a rare error,
                    //       e.g. out of memory, etc.
                    //
                    Interlocked.Increment(ref createWasNotOkCount);
                    error = result;
                }

                if (useCache && (newInterpreter != null))
                {
                    /* NO RESULT */
                    Cleanup(phase, useThreadId);

                    //
                    // NOTE: Forbid the (newly created) interpreter from
                    //       being disposed.  If we cannot, then fail.
                    //
                    if (!ForbidDispose(newInterpreter))
                    {
                        error = String.Format(
                            "cannot disable interpreter {0} disposal",
                            Utility.FormatWrapOrNull(
                                newInterpreter.IdNoThrow));

                        /* NO RESULT */
                        Dispose(ref newInterpreter);

                        return null;
                    }

                    //
                    // NOTE: Store the (newly created) interpreter in the
                    //       interpreter cache.  If we cannot, then fail.
                    //
                    if (!SetCached(
                            phase, newInterpreter, useThreadId, false))
                    {
                        error = String.Format(
                            "cannot save interpreter {0} to the cache",
                            Utility.FormatWrapOrNull(
                                newInterpreter.IdNoThrow));

                        /* NO RESULT */
                        Dispose(ref newInterpreter);

                        return null;
                    }
                }

                return newInterpreter;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes and removes the cached interpreter for the phase.
        /// </summary>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        private static void Cleanup(
            InterpreterPhase phase, /* in */
            bool useThreadId        /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (interpreters == null)
                    return;

                long threadId = GetCacheThreadId(useThreadId);
                PhaseDictionary localInterpreters;

                if (!interpreters.TryGetValue(
                        threadId, out localInterpreters) ||
                    (localInterpreters == null))
                {
                    return;
                }

                Interpreter oldInterpreter;

                if (!localInterpreters.TryGetValue(
                        phase, out oldInterpreter))
                {
                    return;
                }

                /* NO RESULT */
                Dispose(ref oldInterpreter);

                /* IGNORED */
                localInterpreters.Remove(phase);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes stale cached interpreters across all threads, if a cleanup
        /// is pending.
        /// </summary>
        /// <param name="maximumSeconds">
        /// The staleness threshold, in seconds.
        /// </param>
        /// <param name="securityFlags">
        /// The security flags governing cleanup tracing.
        /// </param>
        /// <returns>
        /// Non-zero when cleanup ran; otherwise, zero.
        /// </returns>
        public static bool MaybeCleanupStale(
            long maximumSeconds,        /* in */
            SecurityFlags securityFlags /* in */
            )
        {
            if (maximumSeconds < 0)
                return false; // NOTE: Never?

            if (Interlocked.CompareExchange(ref cleanupPending, 1, 0) != 0)
                return false; // NOTE: Queued -OR- running.

            bool queued = false;

            try
            {
                queued = Utility.QueueUserWorkItem(
                    new WaitCallback(CleanupStaleCallback),
                    new CleanupPair(maximumSeconds, securityFlags));

                return queued;
            }
            finally
            {
                if (!queued)
                {
                    /* IGNORED */
                    Interlocked.CompareExchange(ref cleanupPending, 0, 1);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /* System.Threading.WaitCallback */
        /// <summary>
        /// The thread-pool callback that performs the stale-interpreter
        /// cleanup.
        /// </summary>
        /// <param name="state">
        /// The callback state, if any.
        /// </param>
        private static void CleanupStaleCallback(
            object state /* in */
            )
        {
            if (Interlocked.CompareExchange(
                    ref cleanupPending, 2, 1) != 1)
            {
                //
                // NOTE: Either we are already pending -OR-
                //       there is something weird happening.
                //
                return; // NOTE: Already pending?
            }

            try
            {
                CleanupPair anyPair = state as CleanupPair;

                if (anyPair == null)
                    return;

                long maximumSeconds = anyPair.X;
                SecurityFlags securityFlags = anyPair.Y;

                int count = CleanupAll(null, null, maximumSeconds);

                if (WebLicenseOps.HasFlags(
                        securityFlags, SecurityFlags.TraceCleanup, true))
                {
                    TracePriority priority = (count > 0) ?
                        TracePriority.MediumHigh : TracePriority.Medium;

                    Utility.DebugTrace(String.Format(
                        "CleanupStaleCallback: maximumSeconds = {0}, " +
                        "securityFlags = {1}, count = {2}, status = {3}",
                        maximumSeconds, securityFlags, count,
                        GetCacheStatus()), typeof(InterpreterOps).Name,
                        priority | TracePriority.FromPlugin);
                }
            }
#if DEBUG || FORCE_TRACE
            catch (ThreadAbortException e)
#else
            catch (ThreadAbortException)
#endif
            {
                Thread.ResetAbort();

#if DEBUG || FORCE_TRACE
                Utility.DebugTrace(
                    e, typeof(InterpreterOps).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);
#endif
            }
#if DEBUG || FORCE_TRACE
            catch (Exception e)
#else
            catch (Exception)
#endif
            {
#if DEBUG || FORCE_TRACE
                Utility.DebugTrace(
                    e, typeof(InterpreterOps).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);
#endif
            }
            finally
            {
                /* IGNORED */
                Interlocked.Exchange(ref cleanupPending, 0);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes all cached interpreters matching the thread id, phase, and
        /// staleness criteria.
        /// </summary>
        /// <param name="threadId">
        /// The thread id to match, or a shared value for all.
        /// </param>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="maximumSeconds">
        /// The staleness threshold, in seconds.
        /// </param>
        /// <returns>
        /// The number of interpreters disposed.
        /// </returns>
        private static int CleanupAll(
            long? threadId,          /* in */
            InterpreterPhase? phase, /* in */
            long? maximumSeconds     /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (interpreters == null)
                    return Count.Invalid;

                int count = 0;

                LongList localThreadIds = new LongList(
                    interpreters.Keys);

                foreach (long localThreadId in localThreadIds)
                {
                    if ((threadId != null) &&
                        (localThreadId != (long)threadId))
                    {
                        continue;
                    }

                    PhaseDictionary localInterpreters;

                    if (!interpreters.TryGetValue(
                            localThreadId, out localInterpreters) ||
                        (localInterpreters == null))
                    {
                        continue;
                    }

                    PhaseList localPhases = new PhaseList(
                        localInterpreters.Keys);

                    foreach (InterpreterPhase localPhase in localPhases)
                    {
                        if ((phase != null) &&
                            (localPhase != (InterpreterPhase)phase))
                        {
                            continue;
                        }

                        Interpreter oldInterpreter;

                        if (!localInterpreters.TryGetValue(
                                localPhase, out oldInterpreter))
                        {
                            continue;
                        }

                        if ((maximumSeconds != null) && !IsStale(
                                oldInterpreter, maximumSeconds))
                        {
                            continue;
                        }

                        /* NO RESULT */
                        Dispose(ref oldInterpreter);

                        //
                        // NOTE: Remove the interpreter cache entry
                        //       for this thread / phase -AND- then
                        //       remove the (outer) cache entry for
                        //       this thread, if it is now empty.
                        //
                        if (localInterpreters.Remove(localPhase) &&
                            (localInterpreters.Count == 0))
                        {
                            /* IGNORED */
                            interpreters.Remove(localThreadId);
                        }

                        count++;
                    }
                }

                return count;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Exit Handler Methods
        /// <summary>
        /// Handles the interpreter-exited event by scheduling a cleanup.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private static void ExitedEventHandler(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            /* IGNORED */
            CleanupAll(null, null, null);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the interpreter-exited event handler.
        /// </summary>
        private static void AddExitedEventHandler()
        {
            AppDomain appDomain = AppDomain.CurrentDomain;

            if (appDomain == null)
                return;

            if (appDomain.IsDefaultAppDomain())
            {
                appDomain.ProcessExit -= ExitedEventHandler;
                appDomain.ProcessExit += ExitedEventHandler;
            }
            else
            {
                appDomain.DomainUnload -= ExitedEventHandler;
                appDomain.DomainUnload += ExitedEventHandler;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Server Page Methods
        /// <summary>
        /// Determines whether an interpreter would be created if requested for
        /// the page and phase.
        /// </summary>
        /// <param name="pageData">
        /// The page data describing the requirements.
        /// </param>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <param name="refresh">
        /// Non-zero when a refresh would be requested.
        /// </param>
        /// <returns>
        /// Non-zero when an interpreter would be created; otherwise, zero.
        /// </returns>
        public static bool WillBeCreatedIfRequested(
            IScriptPageData pageData, /* in */
            InterpreterPhase phase,   /* in */
            bool useThreadId,         /* in */
            bool? refresh             /* in */
            )
        {
            if ((refresh != null) && (bool)refresh)
                return true;

            return !HaveCached(pageData, phase, useThreadId);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or creates an interpreter for the page and phase.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments for the interpreter.
        /// </param>
        /// <param name="pageData">
        /// The page data describing the requirements.
        /// </param>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <param name="refresh">
        /// Non-zero to force creation of a fresh interpreter.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The interpreter, or null on failure.
        /// </returns>
        public static Interpreter GetOrCreate(
            ArgsList args,            /* in */
            IScriptPageData pageData, /* in */
            InterpreterPhase phase,   /* in */
            bool useThreadId,         /* in */
            bool? refresh,            /* in */
            ref Result error          /* out */
            )
        {
            bool created; /* NOT USED */

            return GetOrCreate(
                args, pageData, phase, useThreadId, refresh,
                out created, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or creates an interpreter for the page and phase, reporting
        /// whether a new one was created.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments for the interpreter.
        /// </param>
        /// <param name="pageData">
        /// The page data describing the requirements.
        /// </param>
        /// <param name="phase">
        /// The interpreter phase whose cache slot is used.
        /// </param>
        /// <param name="useThreadId">
        /// Non-zero to scope the operation to the current thread.
        /// </param>
        /// <param name="refresh">
        /// Non-zero to force creation of a fresh interpreter.
        /// </param>
        /// <param name="created">
        /// On output, non-zero when a new interpreter was created.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The interpreter, or null on failure.
        /// </returns>
        public static Interpreter GetOrCreate(
            ArgsList args,            /* in */
            IScriptPageData pageData, /* in */
            InterpreterPhase phase,   /* in */
            bool useThreadId,         /* in */
            bool? refresh,            /* in */
            out bool created,         /* out */
            ref Result error          /* out */
            )
        {
            created = false;

            if (pageData == null)
            {
                error = "invalid script page data";
                return null;
            }

            if (!pageData.CreateInterpreter)
            {
                error = "interpreter creation disabled";
                return null;
            }

            return GetOrCreate(
                args, phase, pageData.CacheInterpreter,
                useThreadId, refresh, out created,
                ref error);
        }
        #endregion
    }
}
