/*
 * CertificatePluginState.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;

using PluginNameList = Eagle._Containers.Public.StringList;

using AssemblyFilePluginNames = System.Collections.Generic.Dictionary<
    string, Eagle._Containers.Public.StringList>;

using Int64PluginDictionary =
    System.Collections.Generic.Dictionary<long,
        Eagle._Interfaces.Public.IPlugin>;

using PluginPair = System.Collections.Generic.KeyValuePair<
    Eagle._Interfaces.Public.IInterpreter, object>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Tracks the plugins pending initialization from this assembly, on a
    /// per-interpreter basis, and maintains the mappings between the Harpy
    /// and Badge plugin assembly file names and their contained plugin type
    /// names, for use by the licensing policy subsystem.
    /// </summary>
    [ObjectId("275efb99-6da3-4ac2-aa45-84ae49f9f46f")]
    internal static class CertificatePluginState
    {
        #region Private Constants
        //
        // NOTE: This is the complete list of plugin file names that are
        //       housed within the (various SKUs) of Harpy assemblies.
        //
        /// <summary>
        /// The complete list of plugin file names that are housed within the
        /// various SKUs of the Harpy assemblies.
        /// </summary>
        private static readonly string[] HarpyPluginFileNames = {
            "Harpy.dll", "Harpy.Basic.dll", "Harpy.Limited.dll",
            "Harpy.Sdk.dll"
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the complete list of plugin type names that are
        //       housed within the (various SKUs) of Harpy assemblies.
        //
        /// <summary>
        /// The complete list of plugin type names that are housed within the
        /// various SKUs of the Harpy assemblies.
        /// </summary>
        private static readonly string[] HarpyPluginTypeNames = {
            "Licensing.Core", "Licensing.Standard", "Licensing.Enterprise",
            "Security.Core"
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the complete list of plugin file names that are
        //       housed within the (various SKUs) of Badge assemblies.
        //
        /// <summary>
        /// The complete list of plugin file names that are housed within the
        /// various SKUs of the Badge assemblies.
        /// </summary>
        private static readonly string[] BadgePluginFileNames = {
            "Badge.dll", "Badge.Basic.dll"
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the complete list of plugin type names that are
        //       housed within the (various SKUs) of Badge assemblies.
        //
        /// <summary>
        /// The complete list of plugin type names that are housed within the
        /// various SKUs of the Badge assemblies.
        /// </summary>
        private static readonly string[] BadgePluginTypeNames = {
            "Badge.Enterprise", "Security.Certificates"
        };
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This is used to synchronize access to the private key ring
        //       and key pair data in this class (i.e. which is used by the
        //       policy subsystem).
        //
        /// <summary>
        /// Used to synchronize access to the private key ring and key pair
        /// data in this class, which is used by the policy subsystem.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        //
        // NOTE: This is the list of plugins pending initialization from
        //       this assembly, on a per-interpreter basis.
        //
        /// <summary>
        /// The list of plugins pending initialization from this assembly, on
        /// a per-interpreter basis.
        /// </summary>
        private static readonly InterpreterObjectDictionary plugins =
            new InterpreterObjectDictionary();
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the list of mappings between plugin file names and
        //       their contained plugin type names.
        //
        /// <summary>
        /// The list of mappings between plugin file names and their contained
        /// plugin type names.
        /// </summary>
        private static AssemblyFilePluginNames pluginMappings;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
#if LICENSING
        /// <summary>
        /// Gets the dictionary of pending plugins associated with the
        /// specified interpreter, optionally creating it if it does not
        /// already exist.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose pending plugins are being queried.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the dictionary if it does not already exist.
        /// </param>
        /// <returns>
        /// The dictionary of pending plugins for the interpreter, or null if
        /// it could not be returned.
        /// </returns>
        private static Int64PluginDictionary GetPending( /* CORE? */
            Interpreter interpreter, /* in */
            bool create              /* in */
            )
        {
            Result error = null;

            return GetPending(interpreter, create, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the dictionary of pending plugins associated with the
        /// specified interpreter, optionally creating it if it does not
        /// already exist.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose pending plugins are being queried.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the dictionary if it does not already exist.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the
        /// dictionary could not be returned.
        /// </param>
        /// <returns>
        /// The dictionary of pending plugins for the interpreter, or null if
        /// it could not be returned.
        /// </returns>
        private static Int64PluginDictionary GetPending( /* CORE? */
            Interpreter interpreter, /* in */
            bool create,             /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return null;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (plugins == null)
                {
                    error = "pending plugins not available";
                    return null;
                }

                Int64PluginDictionary dictionary = null;
                object value;

                if (plugins.TryGetValue(interpreter, out value))
                {
                    dictionary = value as Int64PluginDictionary;
                }
                else if (create)
                {
                    dictionary = new Int64PluginDictionary();
                    plugins.Add(interpreter, dictionary);
                }

                if (dictionary == null)
                    error = "no pending plugins for interpreter";

                return dictionary;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the number of pending plugins for the specified interpreter
        /// to the supplied running count.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose pending plugins are being counted.
        /// </param>
        /// <param name="count">
        /// Receives the running total of pending plugins, incremented by the
        /// number found for the interpreter.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the
        /// count could not be obtained.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool CountPending( /* CORE? */
            Interpreter interpreter, /* in */
            ref int count,           /* in, out */
            ref Result error         /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                Int64PluginDictionary dictionary = GetPending(
                    interpreter, false, ref error);

                if (dictionary == null)
                    return false;

#if DEBUG || FORCE_TRACE
                DebugOnlyOps.DumpPendingPlugins(
                    "CountPending", dictionary,
                    typeof(CertificatePluginState).Name,
                    PolicyType.Unknown, TracePriority.High);
#endif

                count += dictionary.Count;
                return true;
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Populates the plugin file name to plugin type name mappings using
        /// the specified file names and type names, optionally forcing
        /// reinitialization and clearing any existing mappings.
        /// </summary>
        /// <param name="force">
        /// Non-zero to reinitialize the mappings even if they have already
        /// been initialized.
        /// </param>
        /// <param name="clear">
        /// Non-zero to clear any existing mappings before adding the
        /// specified ones.
        /// </param>
        /// <param name="fileNames">
        /// The plugin file names to add to the mappings.
        /// </param>
        /// <param name="typeNames">
        /// The plugin type names to associate with each of the specified
        /// file names.
        /// </param>
        private static void InitializeMappings( /* CORE? */
            bool force,                    /* in */
            bool clear,                    /* in */
            IEnumerable<string> fileNames, /* in */
            IEnumerable<string> typeNames  /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (!force && (pluginMappings != null))
                    return;

                if (pluginMappings == null)
                    pluginMappings = new AssemblyFilePluginNames();
                else if (clear)
                    pluginMappings.Clear();

                if (fileNames == null)
                    return;

                PluginNameList localTypeNames = (typeNames != null) ?
                    new PluginNameList(typeNames) : null;

                foreach (string fileName in fileNames)
                {
                    if (fileName == null)
                        continue;

                    pluginMappings[fileName] = localTypeNames;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
#if LICENSING
        /// <summary>
        /// Adds the number of pending plugins for the specified interpreter
        /// to the supplied running count, optionally complaining when the
        /// count cannot be obtained.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose pending plugins are being counted.
        /// </param>
        /// <param name="noComplain">
        /// Non-zero to suppress reporting of any error encountered while
        /// counting.
        /// </param>
        /// <param name="count">
        /// Receives the running total of pending plugins, incremented by the
        /// number found for the interpreter.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool CountPending( /* CORE? */
            Interpreter interpreter, /* in */
            bool noComplain,         /* in */
            ref int count            /* in, out */
            )
        {
            Result error = null;

            if (!CountPending(
                    interpreter, ref count, ref error))
            {
                if (!noComplain)
                {
                    Utility.Complain(
                        interpreter, ReturnCode.Error, error);
                }

                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all pending plugins associated with the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose pending plugins are to be removed.
        /// </param>
        /// <returns>
        /// Non-zero if the pending plugins for the interpreter were removed;
        /// otherwise, zero.
        /// </returns>
        public static bool RemovePending( /* CORE? */
            Interpreter interpreter /* in */
            )
        {
            if (interpreter == null)
                return false;

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (plugins == null)
                    return false;

                return plugins.Remove(interpreter);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the pending plugin associated with the specified interpreter
        /// and thread.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose pending plugin is being queried.
        /// </param>
        /// <param name="threadId">
        /// The identifier of the thread whose pending plugin is being
        /// queried.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message that describes why the
        /// pending plugin could not be returned.
        /// </param>
        /// <returns>
        /// The pending plugin for the specified interpreter and thread, or
        /// null if none is available.
        /// </returns>
        public static IPlugin GetPending( /* CORE? */
            Interpreter interpreter, /* in */
            long threadId,           /* in */
            ref Result error         /* out */
            )
        {
            if (threadId == 0)
            {
                error = "invalid pending plugin thread Id";
                return null;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                Int64PluginDictionary dictionary = GetPending(
                    interpreter, true, ref error);

                if (dictionary == null)
                    return null;

                IPlugin plugin;

                if (!dictionary.TryGetValue(threadId, out plugin))
                    return null;

                return plugin;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds a pending plugin for the specified interpreter and thread.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter for which the pending plugin is being added.
        /// </param>
        /// <param name="threadId">
        /// The identifier of the thread for which the pending plugin is being
        /// added.
        /// </param>
        /// <param name="plugin">
        /// The plugin to add to the pending plugins for the interpreter and
        /// thread.
        /// </param>
        /// <returns>
        /// Non-zero if the pending plugin was added; otherwise, zero.
        /// </returns>
        public static bool AddPending(
            Interpreter interpreter, /* in */
            long threadId,           /* in */
            IPlugin plugin           /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                Int64PluginDictionary dictionary = GetPending(
                    interpreter, true);

                if (dictionary == null)
                    return false;

                dictionary.Add(threadId, plugin);
                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the pending plugin associated with the specified
        /// interpreter and thread.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose pending plugin is to be removed.
        /// </param>
        /// <param name="threadId">
        /// The identifier of the thread whose pending plugin is to be
        /// removed.
        /// </param>
        /// <returns>
        /// Non-zero if the pending plugin was removed; otherwise, zero.
        /// </returns>
        public static bool RemovePending(
            Interpreter interpreter, /* in */
            long threadId            /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                Int64PluginDictionary dictionary = GetPending(
                    interpreter, true);

                if (dictionary == null)
                    return false;

                return dictionary.Remove(threadId);
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the plugin file name to plugin type name mappings for
        /// both the Harpy and Badge assemblies.
        /// </summary>
        /// <param name="force">
        /// Non-zero to reinitialize the mappings even if they have already
        /// been initialized.
        /// </param>
        public static void InitializeMappings( /* CORE? */
            bool force /* in */
            )
        {
            InitializeMappings(
                force, true, HarpyPluginFileNames,
                HarpyPluginTypeNames);

            InitializeMappings(
                force, false, BadgePluginFileNames,
                BadgePluginTypeNames);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and returns a copy of the plugin file name to plugin type
        /// name mappings, initializing them first if necessary.
        /// </summary>
        /// <returns>
        /// A copy of the plugin mappings, or null if they are not available.
        /// </returns>
        public static AssemblyFilePluginNames CopyMappings() /* CORE? */
        {
            InitializeMappings(false);

            lock (syncRoot) /* TRANSACTIONAL */
            {
                return (pluginMappings != null) ?
                    new AssemblyFilePluginNames(pluginMappings) : null;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Cleanup Methods
#if LICENSING
        /// <summary>
        /// Counts the pending plugins belonging to disposed interpreters and,
        /// when any are found, appends a summary of them to the specified
        /// string builder and adds them to the running total.
        /// </summary>
        /// <param name="priority">
        /// The trace priority to use when reporting the pending plugins.
        /// </param>
        /// <param name="builder">
        /// Receives the appended summary of pending plugins; created if null
        /// when there is something to report.
        /// </param>
        /// <param name="totalCount">
        /// Receives the running total, incremented by the number of pending
        /// plugins that were counted.
        /// </param>
        public static void MaybeCountPending(
            TracePriority priority,    /* in */
            ref StringBuilder builder, /* in, out */
            ref int totalCount         /* in, out */
            ) /* CORE? */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (plugins == null)
                    return;

                int count = 0;

                foreach (PluginPair pair in plugins)
                {
                    Interpreter interpreter =
                        pair.Key as Interpreter;

                    if (interpreter == null)
                        continue;

                    if (!interpreter.Disposed)
                        continue;

                    Int64PluginDictionary dictionary =
                        pair.Value as Int64PluginDictionary;

                    if (dictionary != null)
                    {
#if DEBUG || FORCE_TRACE
                        DebugOnlyOps.DumpPendingPlugins(
                            String.Format("MaybeCountPending({0})",
                            CertificateDataOps.FormatInterpreter(
                                interpreter, true, true)), dictionary,
                            typeof(CertificatePluginState).Name,
                            PolicyType.Unknown, priority);
#endif

                        count += dictionary.Count;
                    }
                }

                if (count > 0)
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.AppendFormat(
                        "pendingPlugins(interpreters, {0})", count);

                    totalCount += count;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the pending plugins belonging to disposed interpreters
        /// and, when any are removed, appends a summary of them to the
        /// specified string builder and adds them to the running total.
        /// </summary>
        /// <param name="builder">
        /// Receives the appended summary of removed pending plugins; created
        /// if null when there is something to report.
        /// </param>
        /// <param name="totalCount">
        /// Receives the running total, incremented by the number of pending
        /// plugins that were removed.
        /// </param>
        public static void MaybeCleanupPending(
            ref StringBuilder builder, /* in, out */
            ref int totalCount         /* in, out */
            ) /* CORE? */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (plugins == null)
                    return;

                int count = 0;

                InterpreterList keys = new InterpreterList(
                    plugins.Keys);

                foreach (IInterpreter interpreter in keys)
                {
                    if (interpreter == null)
                        continue;

                    if (!interpreter.Disposed)
                        continue;

                    if (plugins.Remove(interpreter))
                        count++;
                }

                if (count > 0)
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.AppendFormat(
                        "pendingPlugins({0})", count);

                    totalCount += count;
                }
            }
        }
#endif
        #endregion
    }
}
