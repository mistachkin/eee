/*
 * ConfigurationActions.cs --
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

using ConfigurationDictionary = System.Collections.Generic.Dictionary<
    Kapok.Components.Private.ConfigurationAction, System.DateTime>;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Tracks the completion status of one-time-per-AppDomain configuration
    /// actions, so each initialization step runs at most once.
    /// </summary>
    [ObjectId("2d12ecc1-9c25-4e2a-87d0-e3aa6f97fd0a")]
    internal static class ConfigurationActions
    {
        #region Private Static Data
        //
        // NOTE: This lock is used to synchronize access to the "actions"
        //       static field.
        //
        /// <summary>
        /// The object used to synchronize access to the action status table.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This static field is being used to store "done" flags for
        //       the various server configuration actions that performed on
        //       a per-AppDomain and/or per-process basis.
        //
        /// <summary>
        /// The completion times of the configuration actions performed so far.
        /// </summary>
        private static readonly ConfigurationDictionary actions =
            new ConfigurationDictionary();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified configuration action has been
        /// completed.
        /// </summary>
        /// <param name="action">
        /// The configuration action to check.
        /// </param>
        /// <returns>
        /// Non-zero when the action has been completed; otherwise, zero.
        /// </returns>
        public static bool IsDone(
            ConfigurationAction action /* in */
            )
        {
            return IsDone(action, null);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified configuration action has been
        /// completed within the given number of seconds.
        /// </summary>
        /// <param name="action">
        /// The configuration action to check.
        /// </param>
        /// <param name="maximumSeconds">
        /// The maximum age, in seconds, for the completion to count.
        /// </param>
        /// <returns>
        /// Non-zero when the action was completed recently enough; otherwise,
        /// zero.
        /// </returns>
        private static bool IsDone(
            ConfigurationAction action, /* in */
            long? maximumSeconds        /* in: OPTIONAL */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (actions == null) /* REDUNDANT (?) */
                    return false; // NOTE: Not available?

                DateTime then;

                if (!actions.TryGetValue(action, out then))
                    return false; // NOTE: Never done?

                if (maximumSeconds != null)
                {
                    DateTime now = Utility.GetUtcNow();

                    if (now < then)
                        return false; // NOTE: Time travel?

                    long seconds = Convert.ToInt64(
                        now.Subtract(then).TotalSeconds);

                    if (seconds > (long)maximumSeconds)
                        return false; // NOTE: It is "stale".
                }

                return true; // NOTE: Present and not "stale".
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the specified configuration action as completed.
        /// </summary>
        /// <param name="action">
        /// The configuration action to mark.
        /// </param>
        /// <returns>
        /// Non-zero when the action was newly marked; otherwise, zero.
        /// </returns>
        public static bool TryMarkDone(
            ConfigurationAction action /* in */
            )
        {
            return TryMarkDone(action, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks or unmarks the specified configuration action as completed.
        /// </summary>
        /// <param name="action">
        /// The configuration action to mark.
        /// </param>
        /// <param name="mark">
        /// Non-zero to mark the action done; zero to unmark it.
        /// </param>
        /// <returns>
        /// Non-zero when the mark state changed; otherwise, zero.
        /// </returns>
        public static bool TryMarkDone(
            ConfigurationAction action, /* in */
            bool mark                   /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (actions == null) /* REDUNDANT (?) */
                    return false; // NOTE: Not available?

                if (mark == actions.ContainsKey(action))
                    return false;// NOTE: Already marked?

                DateTime now = Utility.GetUtcNow();

                if (mark)
                {
                    actions[action] = now; // NOTE: It is now.

                    Utility.DebugTrace(String.Format(
                        "TryMarkDone: MARKED {0} ==> {1}", action,
                        Utility.FormatIso8601FullDateTime(now)),
                        typeof(ConfigurationActions).Name,
                        TracePriority.MediumHigh |
                            TracePriority.FromPlugin);
                }
                else if (!actions.Remove(action))
                {
                    return false; // NOTE: Failed remove?
                }
                else
                {
                    Utility.DebugTrace(String.Format(
                        "TryMarkDone: UNMARKED {0} ==> {1}", action,
                        Utility.FormatIso8601FullDateTime(now)),
                        typeof(ConfigurationActions).Name,
                        TracePriority.MediumHigh |
                            TracePriority.FromPlugin);
                }

                return true; // NOTE: Success.
            }
        }
    }
}
