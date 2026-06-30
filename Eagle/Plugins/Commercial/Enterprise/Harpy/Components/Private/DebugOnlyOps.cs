/*
 * DebugOnlyOps.cs --
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
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using Licensing.Interfaces.Private;
using Licensing.Interfaces.Public;
using Utility = Eagle._Components.Public.Utility;

using Int64PluginDictionary =
    System.Collections.Generic.Dictionary<
        long, Eagle._Interfaces.Public.IPlugin>;

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
using KeyRingPair = System.Collections.Generic.KeyValuePair<
    string, Licensing.Interfaces.Private.IKeyRing>;
#endif

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides debug-only helper operations used to format and dump
    /// licensing data -- such as <see cref="ICertificate" /> certificates,
    /// <see cref="IKeyPair" /> key pairs, <see cref="IKeyRing" /> key rings,
    /// and pending <see cref="IPlugin" /> plugins -- for inspection within
    /// the debugger.  The output produced by these methods is intended for
    /// diagnostic use only and is not suitable for display to an end-user.
    /// </summary>
    [ObjectId("1adaa988-93f8-479c-9096-b69f00a25422")]
    internal static class DebugOnlyOps
    {
        #region Policy Support Methods
        //
        // NOTE: The formatting performed by this method is intended for use
        //       in the debugger only (i.e. via DebugTrace, etc).  It is not
        //       suitable for display to an end-user.
        //
        /// <summary>
        /// Formats the specified <see cref="ICertificate" /> into a
        /// human-readable <see cref="StringList" />, rendered as a string
        /// suitable for use in the debugger.  The resulting list includes
        /// the certificate identifier, entity type and name, vendor, public
        /// key token, number, and creation and expiration time stamps.
        /// Returns null when <paramref name="certificate" /> is null.
        /// </summary>
        /// <param name="certificate">
        /// The <see cref="ICertificate" /> to format.  May be null.
        /// </param>
        /// <returns>
        /// The formatted string representing the certificate, or null when
        /// <paramref name="certificate" /> is null.
        /// </returns>
        public static string FormatCertificate( /* POLICY */
            ICertificate certificate /* in */
            )
        {
            string result = null;

            if (certificate != null)
            {
                DateTime created = certificate.TimeStamp;
                TimeSpan duration = certificate.Duration;

                DateTime expired = ((duration != TimeSpan.Zero) &&
                    (duration.Ticks > 0)) ? created.Add(duration) :
                    DateTime.MinValue;

                result = StringList.MakeList(
                    "id", CertificateDataOps.FormatId(
                        certificate.Id),
                    "entityType", certificate.EntityType,
                    "entityName", certificate.EntityName,
                    "vendor", certificate.Vendor,
                    "key", CertificateDataOps.FormatPublicKeyToken(
                        certificate.Key, true, true), /* DIAGNOSTICS */
                    "number", CertificateDataOps.FormatHexadecimal(
                        certificate.Number),
                    "created", CertificateDataOps.FormatTimeStamp(
                        created),
                    "expired", CertificateDataOps.FormatTimeStamp(
                        expired, true));
            }

            return Utility.FormatWrapOrNull(result);
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && CERTIFICATE_POLICY
        /// <summary>
        /// Returns the length, in characters, of the text contained in the
        /// specified <see cref="IScript" />.
        /// </summary>
        /// <param name="script">
        /// The <see cref="IScript" /> whose text length is to be returned.
        /// May be null.
        /// </param>
        /// <returns>
        /// The length of the <c>Text</c>, or
        /// <see cref="Length.Invalid" /> when <paramref name="script" /> or
        /// its text is null.
        /// </returns>
        public static int ScriptLength( /* POLICY */
            IScript script /* in */
            )
        {
            if (script == null)
                return Length.Invalid;

            string text = script.Text;

            if (text == null)
                return Length.Invalid;

            return text.Length;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats and emits diagnostic trace output describing the
        /// specified <see cref="IKeyPair" /> key pairs.  This overload
        /// supplies the default <see cref="ILogClientData" /> and then
        /// forwards to the primary <c>DumpKeyPairs</c> overload.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> associated with the operation.
        /// May be null.
        /// </param>
        /// <param name="prefix">
        /// The prefix text to include at the start of the trace output.
        /// </param>
        /// <param name="type">
        /// The descriptive type text used to label the key pairs in the
        /// output.
        /// </param>
        /// <param name="keyPairs">
        /// The <see cref="IKeyPair" /> key pairs to format and dump.
        /// </param>
        /// <param name="category">
        /// The trace category to use for the output.
        /// </param>
        /// <param name="policyType">
        /// The <see cref="PolicyType" /> associated with the key pairs.
        /// </param>
        /// <param name="priority">
        /// The <see cref="TracePriority" /> to use for the output.
        /// </param>
        public static void DumpKeyPairs( /* CORE? */
            Interpreter interpreter,        /* in */
            string prefix,                  /* in */
            string type,                    /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            string category,                /* in */
            PolicyType policyType,          /* in */
            TracePriority priority          /* in */
            )
        {
            DumpKeyPairs(
                interpreter, CertificateTraceOps.LogClientData(false),
                prefix, type, keyPairs, category, policyType, priority);
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // HACK: *SKU* Technically, this method _could_ always be included
        //       as it does not make use of anything else that requires the
        //       certificate policy engine; however, currently, it is being
        //       omitted when the certificate policy engine is absent.  It
        //       is being "logically grouped" with the DumpKeyRings method.
        //
        /// <summary>
        /// Formats and emits diagnostic trace output describing the
        /// specified <see cref="IKeyPair" /> key pairs.  The key pairs are
        /// formatted via <c>CertificateDataOps.FormatKeyPairs</c> and the
        /// resulting text is emitted through
        /// <c>CertificateTraceOps.MaybeLogAndDebugTrace</c>.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> associated with the operation,
        /// used to adjust the trace priority for a transparent proxy.  May
        /// be null.
        /// </param>
        /// <param name="logClientData">
        /// The <see cref="ILogClientData" /> used when emitting the trace
        /// output.
        /// </param>
        /// <param name="prefix">
        /// The prefix text to include at the start of the trace output.
        /// </param>
        /// <param name="type">
        /// The descriptive type text used to label the key pairs in the
        /// output.
        /// </param>
        /// <param name="keyPairs">
        /// The <see cref="IKeyPair" /> key pairs to format and dump.
        /// </param>
        /// <param name="category">
        /// The trace category to use for the output.
        /// </param>
        /// <param name="policyType">
        /// The <see cref="PolicyType" /> associated with the key pairs.
        /// </param>
        /// <param name="priority">
        /// The <see cref="TracePriority" /> to use for the output.
        /// </param>
        public static void DumpKeyPairs( /* CORE? */
            Interpreter interpreter,        /* in */
            ILogClientData logClientData,   /* in */
            string prefix,                  /* in */
            string type,                    /* in */
            IEnumerable<IKeyPair> keyPairs, /* in */
            string category,                /* in */
            PolicyType policyType,          /* in */
            TracePriority priority          /* in */
            )
        {
            string formatted = CertificateDataOps.FormatKeyPairs(
                keyPairs, true);

            if (String.IsNullOrEmpty(prefix))
                prefix = "DumpKeyPairs";

            if (String.IsNullOrEmpty(category))
                category = typeof(DebugOnlyOps).Name;

            if (Configuration.DoesVariableExist(
                    Constants.DumpKeyPairsEnvVarName))
            {
                int adjustment = 2;

                if ((interpreter != null) &&
                    Utility.IsTransparentProxy(interpreter))
                {
                    interpreter.AdjustTracePriority(
                        ref priority, adjustment);
                }
                else
                {
                    Utility.AdjustTracePriority(
                        ref priority, adjustment);
                }
            }

            CertificateTraceOps.MaybeLogAndDebugTrace(
                logClientData, String.Format(
                    "{0}: Found {1} key {2}pairs: {3}",
                    prefix, Utility.FormatWrapOrNull(policyType),
                    type, formatted),
                category, priority, 1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats and emits diagnostic trace output describing the
        /// specified key rings.  This overload supplies the default
        /// <see cref="ILogClientData" /> and then forwards to the primary
        /// <c>DumpKeyRings</c> overload.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> associated with the operation.
        /// May be null.
        /// </param>
        /// <param name="prefix">
        /// The prefix text to include at the start of the trace output.
        /// </param>
        /// <param name="keyRings">
        /// The <see cref="IKeyRing" /> key rings to enumerate and dump.
        /// </param>
        /// <param name="category">
        /// The trace category to use for the output.
        /// </param>
        /// <param name="policyType">
        /// The <see cref="PolicyType" /> associated with the key rings.
        /// </param>
        /// <param name="priority">
        /// The <see cref="TracePriority" /> to use for the output.
        /// </param>
        public static void DumpKeyRings( /* CORE? */
            Interpreter interpreter,           /* in */
            string prefix,                     /* in */
            IEnumerable<KeyRingPair> keyRings, /* in */
            string category,                   /* in */
            PolicyType policyType,             /* in */
            TracePriority priority             /* in */
            )
        {
            DumpKeyRings(
                interpreter, CertificateTraceOps.LogClientData(false),
                prefix, keyRings, category, policyType, priority);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Iterates over the specified <see cref="IKeyRing" /> key rings,
        /// listing the <see cref="IKeyPair" /> key pairs contained in each
        /// ring and emitting diagnostic trace output describing them via the
        /// primary <c>DumpKeyPairs</c> overload.  When
        /// <paramref name="keyRings" /> is null, a single empty dump is
        /// emitted instead.
        /// </summary>
        /// <param name="interpreter">
        /// The <see cref="Interpreter" /> associated with the operation.
        /// May be null.
        /// </param>
        /// <param name="logClientData">
        /// The <see cref="ILogClientData" /> used when emitting the trace
        /// output.
        /// </param>
        /// <param name="prefix">
        /// The prefix text to include at the start of the trace output.
        /// </param>
        /// <param name="keyRings">
        /// The <see cref="IKeyRing" /> key rings to enumerate and dump.
        /// May be null.
        /// </param>
        /// <param name="category">
        /// The trace category to use for the output.
        /// </param>
        /// <param name="policyType">
        /// The <see cref="PolicyType" /> associated with the key rings.
        /// </param>
        /// <param name="priority">
        /// The <see cref="TracePriority" /> to use for the output.
        /// </param>
        public static void DumpKeyRings( /* CORE? */
            Interpreter interpreter,           /* in */
            ILogClientData logClientData,      /* in */
            string prefix,                     /* in */
            IEnumerable<KeyRingPair> keyRings, /* in */
            string category,                   /* in */
            PolicyType policyType,             /* in */
            TracePriority priority             /* in */
            )
        {
            if (keyRings == null)
            {
                DumpKeyPairs(
                    interpreter, prefix, "ring ", null,
                    category, policyType, priority);

                return;
            }

            if (String.IsNullOrEmpty(prefix))
                prefix = "DumpKeyRings";

            foreach (KeyRingPair pair in keyRings)
            {
                IKeyRing keyRing = pair.Value;

                if (keyRing == null)
                    continue;

                IEnumerable<IKeyPair> keyPairs = null;
                Result error = null;

                if (keyRing.List(
                        ref keyPairs, ref error) != ReturnCode.Ok)
                {
                    CertificateTraceOps.MaybeLogAndDebugTrace(
                        String.Format(
                            "DumpKeyRings: error = {0}",
                            Utility.FormatWrapOrNull(error)),
                        category, priority, 0);

                    continue;
                }

                string keyRingName = keyRing.ToString();

                if (keyRingName == null)
                    keyRingName = pair.Key;

                DumpKeyPairs(
                    interpreter, logClientData, prefix,
                    (keyRingName != null) ? String.Format(
                        "ring {0} ", Utility.FormatWrapOrNull(
                        keyRingName)) : "ring ",
                    keyPairs, category, policyType, priority);
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if LICENSING
        /// <summary>
        /// Formats and emits diagnostic trace output describing the
        /// specified pending plugins.  This overload supplies the default
        /// <see cref="ILogClientData" /> and then forwards to the primary
        /// <c>DumpPendingPlugins</c> overload.
        /// </summary>
        /// <param name="prefix">
        /// The prefix text to include at the start of the trace output.
        /// </param>
        /// <param name="plugins">
        /// The pending <see cref="IPlugin" /> plugins, keyed by identifier,
        /// to format and dump.
        /// </param>
        /// <param name="category">
        /// The trace category to use for the output.
        /// </param>
        /// <param name="policyType">
        /// The <see cref="PolicyType" /> associated with the pending
        /// plugins.
        /// </param>
        /// <param name="priority">
        /// The <see cref="TracePriority" /> to use for the output.
        /// </param>
        public static void DumpPendingPlugins( /* CORE? */
            string prefix,                 /* in */
            Int64PluginDictionary plugins, /* in */
            string category,               /* in */
            PolicyType policyType,         /* in */
            TracePriority priority         /* in */
            )
        {
            DumpPendingPlugins(
                prefix, plugins, CertificateTraceOps.LogClientData(false),
                category, policyType, priority);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats and emits diagnostic trace output describing the
        /// specified pending plugins.  The plugins are formatted via
        /// <c>CertificateDataOps.FormatPendingPlugins</c> and the resulting
        /// text is emitted through
        /// <c>CertificateTraceOps.MaybeLogAndDebugTrace</c>.
        /// </summary>
        /// <param name="prefix">
        /// The prefix text to include at the start of the trace output.
        /// </param>
        /// <param name="plugins">
        /// The pending <see cref="IPlugin" /> plugins, keyed by identifier,
        /// to format and dump.
        /// </param>
        /// <param name="logClientData">
        /// The <see cref="ILogClientData" /> used when emitting the trace
        /// output.
        /// </param>
        /// <param name="category">
        /// The trace category to use for the output.
        /// </param>
        /// <param name="policyType">
        /// The <see cref="PolicyType" /> associated with the pending
        /// plugins.
        /// </param>
        /// <param name="priority">
        /// The <see cref="TracePriority" /> to use for the output.
        /// </param>
        public static void DumpPendingPlugins( /* CORE? */
            string prefix,                 /* in */
            Int64PluginDictionary plugins, /* in */
            ILogClientData logClientData,  /* in */
            string category,               /* in */
            PolicyType policyType,         /* in */
            TracePriority priority         /* in */
            )
        {
            string formatted = CertificateDataOps.FormatPendingPlugins(
                plugins, true);

            if (String.IsNullOrEmpty(prefix))
                prefix = "DumpPendingPlugins";

            if (String.IsNullOrEmpty(category))
                category = typeof(DebugOnlyOps).Name;

            CertificateTraceOps.MaybeLogAndDebugTrace(
                logClientData, String.Format(
                    "{0}: Found {1} pending plugins: {2}",
                    prefix, Utility.FormatWrapOrNull(policyType),
                    formatted),
                category, priority, 0);
        }
#endif
#endif
        #endregion
    }
}
