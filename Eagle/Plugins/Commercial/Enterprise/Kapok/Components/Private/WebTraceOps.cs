/*
 * WebTraceOps.cs --
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
using System.IO;
using System.Reflection;
using System.Text;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Kapok.Components.Public;
using Kapok.Components.Shared;
using IOP = Kapok.Components.Private.InterpreterOps;
using StringPair = System.Collections.Generic.KeyValuePair<string, string>;

namespace Kapok.Components.Private
{
    /// <summary>
    /// Provides the diagnostic logging and trace configuration for the server:
    /// setting up the log file and trace listeners, configuring trace
    /// format/priorities/categories, and dumping the environment.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("7080e014-6a32-4b8f-a406-d4720be01c00")]
    internal static class WebTraceOps
    {
        #region Private Constants
#if TEST
        //
        // NOTE: If this environment variable is set [to anything], logging
        //       will not be enabled by this class.
        //
        /// <summary>
        /// The name of the environment variable that disables server logging.
        /// </summary>
        private static readonly string NoLogServerEnvVarName =
            "NoLogServer";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set [to anything], logging
        //       will be forcibly enabled by this class, using a default log
        //       name and log file name if necessary.
        //
        /// <summary>
        /// The name of the environment variable that forces server logging.
        /// </summary>
        private static readonly string ForceLogServerEnvVarName =
            "ForceLogServer";
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set [to anything], tracing
        //       will not be forcibly enabled by this class.
        //
        /// <summary>
        /// The name of the environment variable that disables server tracing.
        /// </summary>
        private static readonly string NoTraceServerEnvVarName =
            "NoTraceServer";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: If this environment variable is set [to anything], tracing
        //       environment variables will not be initialized based on the
        //       global settings.
        //
        /// <summary>
        /// The name of the environment variable that disables settings
        /// tracing.
        /// </summary>
        private static readonly string NoTraceSettingsEnvVarName =
            "NoTraceSettings";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // HACK: This field is used to synchronize access to the various
        //       internal ConfigurationActions protected method calls in
        //       the MaybeSetupLoggingAndTracing method, i.e. to prevent
        //       time-of-check versus time-of-use issues with the IsDone
        //       and TryMarkDone protected method calls.
        //
        /// <summary>
        /// The object used to synchronize access to the trace configuration
        /// state.
        /// </summary>
        private static readonly object syncRoot = new object();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Log / Trace Subsystem Shared Support Methods
        /// <summary>
        /// Determines whether console output should be used.
        /// </summary>
        /// <returns>
        /// Non-zero when the console should be used; otherwise, zero.
        /// </returns>
        public static bool ShouldUseConsole()
        {
            return Utility.IsDotNetCore();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Log Subsystem Support Methods
#if TEST
        /// <summary>
        /// Sets up the log-file trace listener (a one-time-per-AppDomain
        /// action).
        /// </summary>
        /// <param name="listener">
        /// On output, receives the created log listener, if any.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool MaybeSetupLogFile(
            ref TraceListener listener /* out */
            )
        {
            bool useConsole = false;
            string name = null;
            string fileName = null;
            Encoding encoding = null;
            LogFlags? flags = null;

            if (ShouldSetupLogFile(
                    ShouldForceSetupLogFile(), ref useConsole,
                    ref name, ref fileName, ref encoding,
                    ref flags))
            {
                if (listener == null)
                {
                    if (!String.IsNullOrEmpty(fileName))
                    {
                        string directory = Path.GetDirectoryName(
                            fileName);

                        if (!String.IsNullOrEmpty(directory) &&
                            !Directory.Exists(directory))
                        {
                            try
                            {
                                Directory.CreateDirectory(
                                    directory); /* throw */
                            }
                            catch (Exception e)
                            {
                                Utility.Complain(
                                    null, ReturnCode.Error, e);
                            }
                        }
                    }

                    ReturnCode traceCode;
                    Result traceError = null;

                    traceCode = Utility.SetupTraceLogFile(
                        name, fileName, encoding, flags, true,
                        false, useConsole, useConsole, false,
                        ref listener, ref traceError);

                    if (traceCode == ReturnCode.Ok)
                    {
                        return true;
                    }
                    else
                    {
                        Utility.Complain(
                            null, traceCode, traceError);
                    }
                }
            }
            else if (listener != null)
            {
                try
                {
                    listener.Dispose(); /* throw */
                    listener = null;

                    return true;
                }
                catch (Exception e)
                {
                    Utility.Complain(
                        null, ReturnCode.Error, e);
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether log-file setup should be forced.
        /// </summary>
        /// <returns>
        /// Non-zero when setup should be forced; otherwise, zero.
        /// </returns>
        private static bool ShouldForceSetupLogFile()
        {
            if (EnvironmentOps.HaveVariableValue(
                    ForceLogServerEnvVarName))
            {
                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the log file should be set up and resolves its
        /// configuration.
        /// </summary>
        /// <param name="useDefaults">
        /// Non-zero to use the default configuration.
        /// </param>
        /// <param name="useConsole">
        /// Non-zero to also use the console.
        /// </param>
        /// <param name="name">
        /// On output, receives the log name.
        /// </param>
        /// <param name="fileName">
        /// On output, receives the log file name.
        /// </param>
        /// <param name="encoding">
        /// On output, receives the log encoding.
        /// </param>
        /// <param name="flags">
        /// On output, receives the log flags.
        /// </param>
        /// <returns>
        /// Non-zero when the log file should be set up; otherwise, zero.
        /// </returns>
        private static bool ShouldSetupLogFile(
            bool useDefaults,      /* in */
            ref bool useConsole,   /* out */
            ref string name,       /* out */
            ref string fileName,   /* out */
            ref Encoding encoding, /* out */
            ref LogFlags? flags    /* out */
            )
        {
            if (EnvironmentOps.HaveVariableValue(
                    NoLogServerEnvVarName))
            {
                return false;
            }

            string localName = GetLogName();

            if (useDefaults && (localName == null))
                localName = GetDefaultLogName();

            string localFileName = GetLogFileName();

            if (useDefaults && (localFileName == null))
                localFileName = GetDefaultLogFileName();

            useConsole = ShouldUseConsole();

            name = localName;
            fileName = localFileName;
            encoding = GetLogEncoding();
            flags = GetLogFlags();

            return (localFileName != null);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default log name.
        /// </summary>
        /// <returns>
        /// The default log name.
        /// </returns>
        private static string GetDefaultLogName()
        {
            return String.Format("{0}.ExecuteServerHandler:{1}",
                typeof(WebTraceOps).FullName, Utility.GetCurrentThreadId());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured log name.
        /// </summary>
        /// <returns>
        /// The log name.
        /// </returns>
        private static string GetLogName()
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.String;

            string value = WebSettingsOps.GetGlobal(
                "TraceLogName", dataType);

            if (String.IsNullOrEmpty(value))
                return null;

            return value;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the default log file name.
        /// </summary>
        /// <returns>
        /// The default log file name.
        /// </returns>
        private static string GetDefaultLogFileName()
        {
            string directory = null;
            Assembly localAssembly = WebGlobalState.GetAssembly();

        retry:

            if (localAssembly != null)
            {
                string location = localAssembly.Location;

                if (String.IsNullOrEmpty(location))
                {
                    localAssembly = null;
                    goto retry;
                }

                directory = Path.GetDirectoryName(location);

                if (String.IsNullOrEmpty(directory))
                {
                    localAssembly = null;
                    goto retry;
                }
            }
            else
            {
                AppDomain localAppDomain = WebGlobalState.GetAppDomain();

                if (localAppDomain != null)
                    directory = localAppDomain.BaseDirectory;
            }

            if (String.IsNullOrEmpty(directory))
                return null;

            return Path.Combine(directory, String.Format(
                "{0}.{1}.log", typeof(WebTraceOps).FullName,
                Utility.GetCurrentThreadId()));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured log file name.
        /// </summary>
        /// <returns>
        /// The log file name.
        /// </returns>
        private static string GetLogFileName()
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.FileName |
                SettingDataType.NoExists;

            string value = WebSettingsOps.GetGlobal(
                "TraceLogFileName", dataType);

            if (String.IsNullOrEmpty(value))
                return null;

            return value;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured log encoding.
        /// </summary>
        /// <returns>
        /// The log encoding.
        /// </returns>
        private static Encoding GetLogEncoding()
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.EncodingName;

            string value = WebSettingsOps.GetGlobal(
                "TraceLogEncoding", dataType);

            if (String.IsNullOrEmpty(value))
                return null;

            Encoding encoding;
            Result error = null;

            encoding = Utility.GetEncoding(value, ref error);

            if (encoding != null)
                return encoding;

            Utility.Complain(
                null, ReturnCode.Error, error);

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured log flags.
        /// </summary>
        /// <returns>
        /// The log flags, or null when unset.
        /// </returns>
        private static LogFlags? GetLogFlags()
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.Enumeration;

            string value = WebSettingsOps.GetGlobal(
                "TraceLogFlags", dataType);

            if (String.IsNullOrEmpty(value))
                return null;

            object enumValue;
            Result error = null;

            enumValue = Utility.TryParseFlagsEnum(
                null, typeof(LogFlags),
                LogFlags.Default.ToString(),
                value, null, true, false, true,
                ref error);

            if (enumValue is LogFlags)
                return (LogFlags)enumValue;

            Utility.Complain(
                null, ReturnCode.Error, error);

            return null;
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Trace Subsystem Support Methods
        /// <summary>
        /// Formats a name/value dictionary as a list string for tracing.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary to format.
        /// </param>
        /// <returns>
        /// The formatted list.
        /// </returns>
        private static string FormatPairList(
            StringDictionary dictionary /* in */
            )
        {
            if (dictionary == null)
                return Constants.DisplayNull;

            if (dictionary.Count == 0)
                return Constants.DisplayEmpty;

            StringBuilder builder = new StringBuilder();

            foreach (StringPair pair in dictionary)
            {
                builder.AppendLine();

                builder.AppendFormat(
                    "{0}{1} = {2}", Characters.HorizontalTab,
                    Utility.FormatWrapOrNull(pair.Key),
                    Utility.FormatWrapOrNull(pair.Value));
            }

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the trace settings (format, priorities, categories, and
        /// limits).
        /// </summary>
        /// <param name="format">
        /// The trace format.
        /// </param>
        /// <param name="priorities">
        /// The trace priorities.
        /// </param>
        /// <param name="categories">
        /// The trace categories.
        /// </param>
        /// <param name="noLimits">
        /// Non-zero to disable trace limits.
        /// </param>
        private static void ConfigureSettings(
            string format,             /* in: OPTIONAL */
            TracePriority? priorities, /* in: OPTIONAL */
            StringList categories,     /* in: OPTIONAL */
            bool? noLimits             /* in: OPTIONAL */
            )
        {
            bool forceEnabled = false;

            if ((format != null) && Utility.SetEnvironmentVariable(
                    EnvVars.TraceFormat, format))
            {
                forceEnabled = true;
            }

            if ((priorities != null) && Utility.SetEnvironmentVariable(
                    EnvVars.TracePriorities, priorities.ToString()))
            {
                forceEnabled = true;
            }

            if ((categories != null) && Utility.SetEnvironmentVariable(
                    EnvVars.TraceCategories, categories.ToString()))
            {
                forceEnabled = true;
            }

            if (noLimits != null)
            {
                if ((bool)noLimits)
                {
                    /* IGNORED */
                    Utility.SetEnvironmentVariable(
                        EnvVars.NoTraceLimits, 1.ToString());
                }
                else
                {
                    /* IGNORED */
                    Utility.UnsetEnvironmentVariable(
                        EnvVars.NoTraceLimits);
                }
            }

            Interpreter interpreter = IOP.GetCached(
                InterpreterPhase.Configuration, false, false);

            TraceStateType stateType0 =
                TraceStateType.Environment | TraceStateType.Force;

            TraceStateType stateType1 = TraceStateType.None;
            TraceStateType stateType2 = TraceStateType.None;

            if (forceEnabled)
            {
                stateType1 = Utility.ForceTraceEnabledOrDisabled(
                    interpreter, stateType0, true);

                if ((interpreter != null) &&
                    Utility.IsTransparentProxy(interpreter))
                {
                    stateType2 = interpreter.ForceTraceEnabledOrDisabled(
                        interpreter, stateType0, true);
                }
            }

#if false
            Utility.DebugTrace(String.Format(
                "ConfigureSettings: format = {0}, priorities = {1}, " +
                "categories = {2}, noLimits = {3}, forceEnabled = {4}, " +
                "interpreter = {5}, stateType0 = {6}, stateType1 = {7}, " +
                "stateType2 = {8}",
                Utility.FormatWrapOrNull(format),
                Utility.FormatWrapOrNull(priorities),
                Utility.FormatWrapOrNull(categories),
                Utility.FormatWrapOrNull(noLimits),
                Utility.FormatWrapOrNull(forceEnabled),
                Utility.FormatWrapOrNull((interpreter != null) ?
                    interpreter.IdNoThrow.ToString() : null),
                Utility.FormatWrapOrNull(stateType0),
                Utility.FormatWrapOrNull(stateType1),
                Utility.FormatWrapOrNull(stateType2)),
                typeof(WebTraceOps).Name, TracePriority.High |
                    TracePriority.FromPlugin);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Dumps all environment variables to the trace listeners.
        /// </summary>
        public static void DumpEnvironment()
        {
            StringDictionary dictionary = new StringDictionary(
                Environment.GetEnvironmentVariables());

            Utility.DebugTrace(String.Format(
                "DumpEnvironment: {0}", FormatPairList(dictionary)),
                typeof(WebTraceOps).Name, TracePriority.MediumHigh);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Configures the trace settings if not already done (a
        /// one-time-per-AppDomain action).
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool MaybeConfigureSettings()
        {
            string format = null;
            TracePriority? priorities = null;
            StringList categories = null;
            bool? noLimits = null;

            if (ShouldConfigureSettings(ref format,
                    ref priorities, ref categories,
                    ref noLimits))
            {
                ConfigureSettings(
                    format, priorities, categories,
                    noLimits);

                return true;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the trace settings should be configured and
        /// resolves them.
        /// </summary>
        /// <param name="format">
        /// On output, receives the trace format.
        /// </param>
        /// <param name="priorities">
        /// On output, receives the trace priorities.
        /// </param>
        /// <param name="categories">
        /// On output, receives the trace categories.
        /// </param>
        /// <param name="noLimits">
        /// On output, non-zero to disable trace limits.
        /// </param>
        /// <returns>
        /// Non-zero when the settings should be configured; otherwise, zero.
        /// </returns>
        private static bool ShouldConfigureSettings(
            ref string format,             /* out */
            ref TracePriority? priorities, /* out */
            ref StringList categories,     /* out */
            ref bool? noLimits             /* out */
            )
        {
            if (EnvironmentOps.HaveVariableValue(
                    NoTraceSettingsEnvVarName))
            {
                return false;
            }

            format = GetFormat();
            priorities = GetPriorities();
            categories = GetCategories();
            noLimits = GetNoLimits();

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets up the trace listeners (a one-time-per-AppDomain action).
        /// </summary>
        /// <param name="listener">
        /// On output, receives the created listener, if any.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool MaybeSetupListeners(
            ref TraceListener listener /* out */
            )
        {
            bool useConsole = false;

            if (ShouldSetupListeners(ref useConsole))
            {
                if (listener == null)
                {
                    ReturnCode traceCode;
                    Result traceError = null;

                    traceCode = Utility.SetupTraceListeners(
                        Utility.GetTraceListenerType(useConsole),
                        null, true, false, useConsole, useConsole,
                        true, ref listener, ref traceError);

                    if (traceCode == ReturnCode.Ok)
                    {
                        return true;
                    }
                    else
                    {
                        Utility.Complain(
                            null, traceCode, traceError);
                    }
                }
            }
            else if (listener != null)
            {
                try
                {
                    listener.Dispose(); /* throw */
                    listener = null;

                    return true;
                }
                catch (Exception e)
                {
                    Utility.Complain(
                        null, ReturnCode.Error, e);
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the trace listeners should be set up.
        /// </summary>
        /// <param name="useConsole">
        /// Non-zero when console output is in use.
        /// </param>
        /// <returns>
        /// Non-zero when listeners should be set up; otherwise, zero.
        /// </returns>
        private static bool ShouldSetupListeners(
            ref bool useConsole /* out */
            )
        {
            if (EnvironmentOps.HaveVariableValue(
                    NoTraceServerEnvVarName))
            {
                return false;
            }

            useConsole = ShouldUseConsole();

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured trace format.
        /// </summary>
        /// <returns>
        /// The trace format.
        /// </returns>
        private static string GetFormat()
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.Enumeration;

            string value = WebSettingsOps.GetGlobal(
                EnvVars.TraceFormat, dataType);

            if (String.IsNullOrEmpty(value))
                return null;

            return value;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured trace priorities.
        /// </summary>
        /// <returns>
        /// The trace priorities, or null when unset.
        /// </returns>
        private static TracePriority? GetPriorities()
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.Enumeration;

            string value = WebSettingsOps.GetGlobal(
                EnvVars.TracePriorities, dataType);

            if (String.IsNullOrEmpty(value))
                return null;

            object enumValue;
            Result error = null;

            enumValue = Utility.TryParseFlagsEnum(
                null, typeof(TracePriority),
                TracePriority.DefaultMask.ToString(),
                value, null, true, false, true,
                ref error);

            if (enumValue is TracePriority)
                return (TracePriority)enumValue;

            Utility.Complain(
                null, ReturnCode.Error, error);

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured trace categories.
        /// </summary>
        /// <returns>
        /// The trace categories.
        /// </returns>
        private static StringList GetCategories()
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.StringListMask;

            string value = WebSettingsOps.GetGlobal(
                EnvVars.TraceCategories, dataType);

            if (String.IsNullOrEmpty(value))
                return null;

            StringList list = null;
            Result error = null;

            if (Parser.SplitList(
                    null, value, 0, Length.Invalid, true,
                    ref list, ref error) == ReturnCode.Ok)
            {
                return list;
            }

            Utility.Complain(
                null, ReturnCode.Error, error);

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the configured no-limits flag.
        /// </summary>
        /// <returns>
        /// The no-limits flag, or null when unset.
        /// </returns>
        private static bool? GetNoLimits()
        {
            SettingDataType dataType =
                SettingDataType.DefaultAndExpand |
                SettingDataType.Boolean;

            string value = WebSettingsOps.GetGlobal(
                EnvVars.NoTraceLimits, dataType);

            if (String.IsNullOrEmpty(value))
                return null;

            bool boolValue = false;
            Result error = null;

            if (Value.GetBoolean2(
                    value, ValueFlags.AnyBoolean, null,
                    ref boolValue, ref error) == ReturnCode.Ok)
            {
                return boolValue;
            }

            Utility.Complain(
                null, ReturnCode.Error, error);

            return null;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the object used to synchronize access to the trace
        /// configuration state.
        /// </summary>
        /// <returns>
        /// The synchronization object.
        /// </returns>
        public static object GetSyncRoot()
        {
            return syncRoot;
        }
        #endregion
    }
}
