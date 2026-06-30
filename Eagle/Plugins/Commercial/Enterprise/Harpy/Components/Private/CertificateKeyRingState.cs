/*
 * CertificateKeyRingState.cs --
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
using Licensing.Interfaces.Private;

using KeyRingDictionary =
    System.Collections.Generic.Dictionary<string,
        Licensing.Interfaces.Private.IKeyRing>;

using KeyRingPair = System.Collections.Generic.KeyValuePair<
    Eagle._Interfaces.Public.IInterpreter, object>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Maintains the per-interpreter trusted key ring and key ring file name
    /// state used by the licensing policy subsystem.  All access to this
    /// state is synchronized.
    /// </summary>
    [ObjectId("359d1f77-abf9-4c7e-8786-61f63136d535")]
    internal static class CertificateKeyRingState
    {
        #region Private Data
        //
        // NOTE: This is used to synchronize access to the private key ring
        //       and key pair data in this class (i.e. which is used by the
        //       policy subsystem).
        //
        /// <summary>
        /// Synchronizes access to the trusted key ring and key ring file
        /// name data maintained by this class.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the list of RSA key pairs, on a per-interpreter
        //       basis, that will be trusted in all appropriate contexts,
        //       based on key usage parameters, by the security policies.
        //
        /// <summary>
        /// Holds the set of trusted RSA key rings, on a per-interpreter
        /// basis, keyed by key ring name.
        /// </summary>
        private static readonly InterpreterObjectDictionary keyRings =
            new InterpreterObjectDictionary();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the list of key ring files that have been loaded
        //       within this AppDomain.
        //
        /// <summary>
        /// Holds the key ring file names that have been loaded, on a
        /// per-interpreter basis, keyed by hash value.
        /// </summary>
        private static readonly InterpreterObjectDictionary fileNames =
            new InterpreterObjectDictionary();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Check Skipping Support Methods
#if LICENSING
        /// <summary>
        /// Determines whether the security policy feature checks may be
        /// skipped in the current context.
        /// </summary>
        /// <param name="reason">
        /// Upon return, receives the reason why the checks may be skipped,
        /// when this method returns non-zero.
        /// </param>
        /// <returns>
        /// Non-zero if the policy feature checks may be skipped; otherwise,
        /// zero.
        /// </returns>
        public static bool CanSkipPolicyFeatureChecks( /* CORE? */
            ref string reason /* out */
            )
        {
#if NETWORK && CERTIFICATE_RENEWAL
            if (IsRenewalPending())
            {
                reason = Constants.RenewalSkipReason;
                return true;
            }
#endif

#if DEMO_KEY_PAIRS || DEMO_EDITION
            if (CertificateDemoMode.IsEnabled() &&
                CertificateDemoState.IsLicensePending())
            {
                reason = Constants.DemoSkipReason;
                return true;
            }
#endif

#if !LIMITED_EDITION
            if (CertificateGlobalState.IsPromotionalOrAll())
            {
                reason = Constants.PromotionalSkipReason;
                return true;
            }
#endif

            if (CertificateLicenseState.CanSkip(LicenseType.Feature))
            {
                reason = Constants.FeatureSkipReason;
                return true;
            }

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.MaybeLogAndDebugTrace(
                "Cannot skip policy feature checks.",
                typeof(CertificateKeyRingState).Name,
                TracePriority.MediumLow, 0);
#endif

            return false;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the license certificate checks may be skipped
        /// in the current context.
        /// </summary>
        /// <param name="reason">
        /// Upon return, receives the reason why the checks may be skipped,
        /// when this method returns non-zero.
        /// </param>
        /// <returns>
        /// Non-zero if the license certificate checks may be skipped;
        /// otherwise, zero.
        /// </returns>
        public static bool CanSkipLicenseCertificateChecks( /* CORE? */
            ref string reason /* out */
            )
        {
#if NETWORK && CERTIFICATE_RENEWAL
            if (IsRenewalPending())
            {
                reason = Constants.RenewalSkipReason;
                return true;
            }
#endif

#if DEMO_KEY_PAIRS || DEMO_EDITION
            if (CertificateDemoMode.IsEnabled() &&
                CertificateDemoState.IsLicensePending())
            {
                reason = Constants.DemoSkipReason;
                return true;
            }
#endif

#if !LIMITED_EDITION
            if (CertificateGlobalState.IsPromotionalOrAll())
            {
                reason = Constants.PromotionalSkipReason;
                return true;
            }
#endif

#if DEBUG || FORCE_TRACE
            CertificateTraceOps.MaybeLogAndDebugTrace(
                String.Format(
                    "Cannot skip package certificate checks.{0}",
                    CertificateLicenseState.IsPending() ?
                        "  License certificate verification is pending." :
                        String.Empty),
                typeof(CertificateKeyRingState).Name,
                TracePriority.MediumLow, 0);
#endif

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending Any Count Methods
        /// <summary>
        /// Determines whether any key ring or license key ring operation is
        /// currently pending.
        /// </summary>
        /// <returns>
        /// Non-zero if any key ring or license key ring operation is
        /// pending; otherwise, zero.
        /// </returns>
        public static bool IsAnyPending() /* CORE? */
        {
            if (IsPending())
                return true;

            if (IsLicensePending())
                return true;

            return false;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending Count Methods
        /// <summary>
        /// Determines whether a key ring operation is currently pending.
        /// </summary>
        /// <returns>
        /// Non-zero if a key ring operation is pending; otherwise, zero.
        /// </returns>
        public static bool IsPending() /* CORE? */
        {
            return CertificateProcessOps.IsPending(
                Constants.PendingKeyRingCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the beginning of a pending key ring operation.
        /// </summary>
        public static void BeginPending() /* CORE? */
        {
            CertificateProcessOps.BeginPending(
                Constants.PendingKeyRingCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the end of a pending key ring operation.
        /// </summary>
        public static void EndPending() /* CORE? */
        {
            CertificateProcessOps.EndPending(
                Constants.PendingKeyRingCountEnvVarName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending License Count Methods
        /// <summary>
        /// Determines whether a license key ring operation is currently
        /// pending.
        /// </summary>
        /// <returns>
        /// Non-zero if a license key ring operation is pending; otherwise,
        /// zero.
        /// </returns>
        public static bool IsLicensePending() /* CORE? */
        {
            return CertificateProcessOps.IsPending(
                Constants.PendingLicenseKeyRingCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the beginning of a pending license key ring operation.
        /// </summary>
        public static void BeginLicensePending() /* CORE? */
        {
            CertificateProcessOps.BeginPending(
                Constants.PendingLicenseKeyRingCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the end of a pending license key ring operation.
        /// </summary>
        public static void EndLicensePending() /* CORE? */
        {
            CertificateProcessOps.EndPending(
                Constants.PendingLicenseKeyRingCountEnvVarName);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending Renewal Count Methods
#if NETWORK && CERTIFICATE_RENEWAL
        /// <summary>
        /// Determines whether a key ring renewal operation is currently
        /// pending.
        /// </summary>
        /// <returns>
        /// Non-zero if a key ring renewal operation is pending; otherwise,
        /// zero.
        /// </returns>
        public static bool IsRenewalPending() /* CORE? */
        {
            return CertificateProcessOps.IsPending(
                Constants.PendingRenewalKeyRingCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the beginning of a pending key ring renewal operation.
        /// </summary>
        public static void BeginRenewalPending() /* CORE? */
        {
            CertificateProcessOps.BeginPending(
                Constants.PendingRenewalKeyRingCountEnvVarName);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marks the end of a pending key ring renewal operation.
        /// </summary>
        public static void EndRenewalPending() /* CORE? */
        {
            CertificateProcessOps.EndPending(
                Constants.PendingRenewalKeyRingCountEnvVarName);
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Trusted Data Methods
        #region Private Methods
        /// <summary>
        /// Gets the dictionary of trusted key rings for the specified
        /// interpreter, optionally creating it when it does not yet exist.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trusted key rings are to be returned.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the dictionary of trusted key rings when one
        /// does not already exist for the interpreter.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The dictionary of trusted key rings for the interpreter, or null
        /// when it is not available.
        /// </returns>
        private static KeyRingDictionary GetAllTrusted( /* CORE? */
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
                if (keyRings == null)
                {
                    error = "key rings not available";
                    return null;
                }

                KeyRingDictionary dictionary = null;
                object value;

                if (keyRings.TryGetValue(interpreter, out value))
                {
                    dictionary = value as KeyRingDictionary;
                }
                else if (create)
                {
                    dictionary = new KeyRingDictionary();
                    keyRings.Add(interpreter, dictionary);
                }

                if (dictionary == null)
                    error = "no key rings for interpreter";

                return dictionary;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the specified source and target refer to the
        /// same trusted key ring (i.e. the same interpreter and key ring
        /// name).
        /// </summary>
        /// <param name="sourceInterpreter">
        /// The source interpreter to compare.
        /// </param>
        /// <param name="targetInterpreter">
        /// The target interpreter to compare.
        /// </param>
        /// <param name="sourceName">
        /// The source key ring name to compare.
        /// </param>
        /// <param name="targetName">
        /// The target key ring name to compare.
        /// </param>
        /// <returns>
        /// Non-zero if the source and target refer to the same trusted key
        /// ring; otherwise, zero.
        /// </returns>
        private static bool IsSameTrusted( /* CORE? */
            Interpreter sourceInterpreter, /* in */
            Interpreter targetInterpreter, /* in */
            string sourceName,             /* in */
            string targetName              /* in */
            )
        {
            if (!Object.ReferenceEquals(
                    sourceInterpreter, targetInterpreter))
            {
                return false;
            }

            if (!CertificateDataOps.StringEquals(
                    sourceName, targetName))
            {
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Counts the trusted key rings associated with the specified
        /// interpreter, adding the result to <paramref name="count" />.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trusted key rings are to be counted.
        /// </param>
        /// <param name="count">
        /// Receives the running total of trusted key rings; the count for
        /// the interpreter is added to this value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode CountAllTrusted(
            Interpreter interpreter, /* in */
            ref int count,           /* in, out */
            ref Result error         /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                KeyRingDictionary dictionary = GetAllTrusted(
                    interpreter, false, ref error);

                if (dictionary == null)
                    return ReturnCode.Error;

#if DEBUG || FORCE_TRACE
                DebugOnlyOps.DumpKeyRings(
                    interpreter, "CountAllTrusted", dictionary,
                    typeof(CertificateKeyRingState).Name,
                    PolicyType.Unknown, TracePriority.High);
#endif

                count += dictionary.Count;
                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all trusted key rings associated with the specified
        /// interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trusted key rings are to be removed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode RemoveAllTrusted(
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyRings == null)
                {
                    error = "key rings not available";
                    return ReturnCode.Error;
                }

                if (!keyRings.Remove(interpreter))
                {
                    error = "no key rings for interpreter";
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all trusted key rings for every interpreter.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode RemoveAllTrusted(
            ref Result error /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyRings == null)
                {
                    error = "key rings not available";
                    return ReturnCode.Error;
                }

                keyRings.Clear();
                return ReturnCode.Ok;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Counts the trusted key rings associated with the specified
        /// interpreter, optionally complaining when an error occurs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trusted key rings are to be counted.
        /// </param>
        /// <param name="noComplain">
        /// Non-zero to suppress complaining when an error is encountered.
        /// </param>
        /// <param name="count">
        /// Receives the running total of trusted key rings; the count for
        /// the interpreter is added to this value.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool CountAllTrusted( /* CORE? */
            Interpreter interpreter, /* in */
            bool noComplain,         /* in */
            ref int count            /* in, out */
            )
        {
            ReturnCode code;
            Result error = null;

            code = CountAllTrusted(interpreter, ref count, ref error);

            if ((code != ReturnCode.Ok) && !noComplain)
                Utility.Complain(interpreter, code, error);

            return (code == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all trusted key rings associated with the specified
        /// interpreter, optionally complaining when an error occurs.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose trusted key rings are to be removed.
        /// </param>
        /// <param name="noComplain">
        /// Non-zero to suppress complaining when an error is encountered.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool RemoveAllTrusted( /* CORE? */
            Interpreter interpreter, /* in */
            bool noComplain          /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = RemoveAllTrusted(interpreter, ref error);

            if ((code != ReturnCode.Ok) && !noComplain)
                Utility.Complain(interpreter, code, error);

            return (code == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all trusted key rings for every interpreter, optionally
        /// complaining when an error occurs.
        /// </summary>
        /// <param name="noComplain">
        /// Non-zero to suppress complaining when an error is encountered.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool RemoveAllTrusted( /* CORE? */
            bool noComplain /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = RemoveAllTrusted(ref error);

            if ((code != ReturnCode.Ok) && !noComplain)
                Utility.Complain(null, code, error);

            return (code == ReturnCode.Ok);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the trusted key ring with the specified name for the
        /// specified interpreter, creating it when it does not yet exist.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the trusted key ring.
        /// </param>
        /// <param name="name">
        /// The name of the trusted key ring to return.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// The trusted key ring, or null when it is not available.
        /// </returns>
        public static IKeyRing GetTrusted( /* CORE? */
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            if (name == null)
            {
                error = "invalid key ring name";
                return null;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                KeyRingDictionary dictionary = GetAllTrusted(
                    interpreter, true, ref error);

                if (dictionary == null)
                    return null;

                IKeyRing keyRing;

                if (!dictionary.TryGetValue(name, out keyRing))
                {
                    keyRing = new KeyRing();
                    dictionary.Add(name, keyRing);
                }

                return keyRing;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the trusted key ring with the specified name for the
        /// specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the trusted key ring.
        /// </param>
        /// <param name="name">
        /// The name of the trusted key ring to remove.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode RemoveTrusted(
            Interpreter interpreter, /* in */
            string name,             /* in */
            ref Result error         /* out */
            )
        {
            if (name == null)
            {
                error = "invalid key ring name";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                KeyRingDictionary dictionary = GetAllTrusted(
                    interpreter, false, ref error);

                if (dictionary == null)
                    return ReturnCode.Error;

                if (!dictionary.Remove(name))
                {
                    error = String.Format(
                        "key ring {0} for interpreter not found",
                        Utility.FormatWrapOrNull(name));

                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves a copy of the trusted key ring with the specified source
        /// name under a newly generated name for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the trusted key ring.
        /// </param>
        /// <param name="sourceName">
        /// The name of the trusted key ring to copy.
        /// </param>
        /// <param name="targetName">
        /// Upon success, receives the newly generated name of the saved key
        /// ring.
        /// </param>
        /// <param name="errorOnAlreadyPresent">
        /// Non-zero to return an error when a key ring with the generated
        /// name is already present.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode SaveTrusted(
            Interpreter interpreter,    /* in */
            string sourceName,          /* in */
            ref string targetName,      /* out */
            bool errorOnAlreadyPresent, /* in */
            ref Result error            /* out */
            )
        {
            if (sourceName == null)
            {
                error = "invalid source key ring name";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                KeyRingDictionary dictionary = GetAllTrusted(
                    interpreter, false, ref error);

                if (dictionary == null)
                    return ReturnCode.Error;

                IKeyRing keyRing;

                if (!dictionary.TryGetValue(
                        sourceName, out keyRing))
                {
                    error = String.Format(
                        "key ring {0} for interpreter not found",
                        Utility.FormatWrapOrNull(sourceName));

                    return ReturnCode.Error;
                }

                string newName = Utility.FormatId("saved",
                    typeof(IKeyRing).Name, (interpreter != null) ?
                    interpreter.NextId() : Utility.NextId());

                if (errorOnAlreadyPresent &&
                    dictionary.ContainsKey(newName))
                {
                    error = String.Format(
                        "key ring {0} for interpreter already present",
                        Utility.FormatWrapOrNull(newName));

                    return ReturnCode.Error;
                }

                dictionary[newName] = new KeyRing(keyRing);
                targetName = newName;

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the trusted key ring with the specified source name by
        /// renaming it to the specified target name for the interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the trusted key ring.
        /// </param>
        /// <param name="sourceName">
        /// The current name of the trusted key ring to restore.
        /// </param>
        /// <param name="targetName">
        /// The name to which the trusted key ring is to be restored.
        /// </param>
        /// <param name="errorOnAlreadyPresent">
        /// Non-zero to return an error when a key ring with the target name
        /// is already present.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode RestoreTrusted(
            Interpreter interpreter,    /* in */
            string sourceName,          /* in */
            string targetName,          /* in */
            bool errorOnAlreadyPresent, /* in */
            ref Result error            /* out */
            )
        {
            if (sourceName == null)
            {
                error = "invalid source key ring name";
                return ReturnCode.Error;
            }

            if (targetName == null)
            {
                error = "invalid target key ring name";
                return ReturnCode.Error;
            }

            if (CertificateDataOps.StringEquals(
                    sourceName, targetName))
            {
                error = "source and target key name cannot be the same";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                KeyRingDictionary dictionary = GetAllTrusted(
                    interpreter, false, ref error);

                if (dictionary == null)
                    return ReturnCode.Error;

                IKeyRing keyRing;

                if (!dictionary.TryGetValue(
                        sourceName, out keyRing))
                {
                    error = String.Format(
                        "key ring {0} for interpreter not found",
                        Utility.FormatWrapOrNull(sourceName));

                    return ReturnCode.Error;
                }

                if (errorOnAlreadyPresent &&
                    dictionary.ContainsKey(targetName))
                {
                    error = String.Format(
                        "key ring {0} for interpreter already present",
                        Utility.FormatWrapOrNull(targetName));

                    return ReturnCode.Error;
                }

                dictionary[targetName] = keyRing;
                dictionary.Remove(sourceName);

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Merges the first matching trusted key ring named
        /// <paramref name="sourceName" /> from any interpreter into the
        /// target key ring for the specified target interpreter.
        /// </summary>
        /// <param name="targetInterpreter">
        /// The interpreter that owns the target key ring.
        /// </param>
        /// <param name="sourceName">
        /// The name of the source key ring to merge from.
        /// </param>
        /// <param name="targetName">
        /// The name of the target key ring to merge into.
        /// </param>
        /// <param name="nonEmpty">
        /// Non-zero to consider only source key rings that are non-empty.
        /// </param>
        /// <param name="overwrite">
        /// Non-zero to overwrite existing entries in the target key ring.
        /// </param>
        /// <param name="allowDuplicate">
        /// Non-zero to allow duplicate entries when merging.
        /// </param>
        /// <param name="merged">
        /// Receives the running total of entries that were merged.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode MergeAnyTrusted( /* CORE? */
            Interpreter targetInterpreter, /* in */
            string sourceName,             /* in */
            string targetName,             /* in */
            bool nonEmpty,                 /* in */
            bool overwrite,                /* in */
            bool allowDuplicate,           /* in */
            ref int merged,                /* in, out */
            ref Result error               /* out */
            )
        {
            if (sourceName == null)
            {
                error = "invalid source key ring name";
                return ReturnCode.Error;
            }

            if (targetName == null)
            {
                error = "invalid target key ring name";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyRings == null)
                {
                    error = "key rings not available";
                    return ReturnCode.Error;
                }

                foreach (KeyRingPair pair in keyRings)
                {
                    //
                    // NOTE: Make sure the interpreters or key
                    //       ring names are different (i.e. do
                    //       not attempt to merge to self).
                    //
                    Interpreter sourceInterpreter =
                        pair.Key as Interpreter;

                    if (IsSameTrusted(
                            sourceInterpreter, targetInterpreter,
                            sourceName, targetName))
                    {
                        continue;
                    }

                    KeyRingDictionary dictionary =
                        pair.Value as KeyRingDictionary;

                    if (dictionary == null)
                        continue;

                    IKeyRing sourceKeyRing;

                    if (!dictionary.TryGetValue(
                            sourceName, out sourceKeyRing))
                    {
                        continue;
                    }

                    if (sourceKeyRing == null)
                        continue;

                    if (!nonEmpty || sourceKeyRing.IsNonEmpty())
                    {
                        IKeyRing targetKeyRing = GetTrusted(
                            targetInterpreter, targetName,
                            ref error);

                        if (targetKeyRing == null)
                            return ReturnCode.Error;

                        if (targetKeyRing.Merge(
                                sourceKeyRing, overwrite,
                                allowDuplicate, ref merged,
                                ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }

                        return ReturnCode.Ok;
                    }
                }

                error = String.Format(
                    "no {0}{1} key rings for any interpreter",
                    nonEmpty ? "non-empty " : String.Empty,
                    Utility.FormatWrapOrNull(sourceName));

                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the trusted key ring with the specified source name from
        /// the source interpreter to the specified target name for the
        /// target interpreter.
        /// </summary>
        /// <param name="sourceInterpreter">
        /// The interpreter that owns the source key ring.
        /// </param>
        /// <param name="targetInterpreter">
        /// The interpreter that will own the copied key ring.
        /// </param>
        /// <param name="sourceName">
        /// The name of the source key ring to copy.
        /// </param>
        /// <param name="targetName">
        /// The name of the target key ring to create.
        /// </param>
        /// <param name="errorOnCrossAppDomain">
        /// Non-zero to return an error when the source and target
        /// interpreters reside in different application domains.
        /// </param>
        /// <param name="errorOnNotFound">
        /// Non-zero to return an error when the source key ring cannot be
        /// found.
        /// </param>
        /// <param name="errorOnAlreadyPresent">
        /// Non-zero to return an error when the target key ring is already
        /// present.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode CopyTrusted( /* CORE? */
            Interpreter sourceInterpreter, /* in */
            Interpreter targetInterpreter, /* in */
            string sourceName,             /* in */
            string targetName,             /* in */
            bool errorOnCrossAppDomain,    /* in */
            bool errorOnNotFound,          /* in */
            bool errorOnAlreadyPresent,    /* in */
            ref Result error               /* out */
            )
        {
            if (sourceInterpreter == null)
            {
                error = "invalid source interpreter";
                return ReturnCode.Error;
            }

            if (targetInterpreter == null)
            {
                error = "invalid target interpreter";
                return ReturnCode.Error;
            }

            if (sourceName == null)
            {
                error = "invalid source key ring name";
                return ReturnCode.Error;
            }

            if (targetName == null)
            {
                error = "invalid target key ring name";
                return ReturnCode.Error;
            }

            if (CertificatePolicyOps.IsCrossAppDomain(
                    sourceInterpreter, targetInterpreter))
            {
                if (errorOnCrossAppDomain)
                {
                    error = "cannot copy key ring across domain boundary";
                    return ReturnCode.Error;
                }
                else
                {
                    return ReturnCode.Ok;
                }
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                Result localError = null;

                KeyRingDictionary sourceDictionary = GetAllTrusted(
                    sourceInterpreter, false, ref localError);

                if (sourceDictionary == null)
                {
                    if (errorOnNotFound)
                    {
                        error = localError;
                        return ReturnCode.Error;
                    }
                    else
                    {
                        return ReturnCode.Ok;
                    }
                }

                IKeyRing keyRing;

                if (!sourceDictionary.TryGetValue(
                        sourceName, out keyRing))
                {
                    if (errorOnNotFound)
                    {
                        error = String.Format(
                            "key ring {0} for source interpreter not found",
                            Utility.FormatWrapOrNull(sourceName));

                        return ReturnCode.Error;
                    }
                    else
                    {
                        return ReturnCode.Ok;
                    }
                }

                KeyRingDictionary targetDictionary = GetAllTrusted(
                    targetInterpreter, true, ref error);

                if (targetDictionary == null)
                    return ReturnCode.Error;

                if (errorOnAlreadyPresent &&
                    targetDictionary.ContainsKey(targetName))
                {
                    error = String.Format(
                        "key ring {0} for target interpreter already present",
                        Utility.FormatWrapOrNull(targetName));

                    return ReturnCode.Error;
                }

                targetDictionary[targetName] = new KeyRing(keyRing);
                return ReturnCode.Ok;
            }
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region File Support Methods
        /// <summary>
        /// Clears the trusted key ring file names associated with the
        /// specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose key ring file names are to be cleared.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode ClearFiles(
            Interpreter interpreter /* in */
            )
        {
            Result error = null;

            return ClearFiles(interpreter, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the trusted key ring file names associated with the
        /// specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose key ring file names are to be cleared.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode ClearFiles(
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (fileNames == null)
                {
                    error = "key ring file names not available";
                    return ReturnCode.Error;
                }

                if (!fileNames.Remove(interpreter))
                {
                    error = "no key ring file names for interpreter";
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        #region Dead Code
#if DEAD_CODE
        /// <summary>
        /// Clears the trusted key ring file names for every interpreter.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode ClearFiles(
            ref Result error /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (fileNames == null)
                {
                    error = "key ring file names not available";
                    return ReturnCode.Error;
                }

                fileNames.Clear();
                return ReturnCode.Ok;
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the trusted key ring file name associated with the specified
        /// hash value for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that owns the key ring file name.
        /// </param>
        /// <param name="hashValue">
        /// The hash value identifying the key ring file.
        /// </param>
        /// <param name="fileName">
        /// Upon success, receives the trusted key ring file name.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode GetFile(
            Interpreter interpreter, /* in */
            byte[] hashValue,        /* in */
            ref string fileName,     /* out */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (hashValue == null)
            {
                error = "invalid hash value";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (fileNames == null)
                {
                    error = "key ring file names not available";
                    return ReturnCode.Error;
                }

                StringDictionary dictionary = null;
                object value;

                if (fileNames.TryGetValue(interpreter, out value))
                    dictionary = value as StringDictionary;

                if (dictionary == null)
                {
                    error = "no key ring file names for interpreter";
                    return ReturnCode.Error;
                }

                string key = CertificateDataOps.FormatHexadecimal(
                    hashValue, true);

                if (key == null)
                {
                    error = "invalid key from hash value";
                    return ReturnCode.Error;
                }

                string localFileName;

                if (dictionary.TryGetValue(key, out localFileName))
                {
                    fileName = localFileName;
                    return ReturnCode.Ok;
                }
                else
                {
                    error = String.Format(
                        "key ring file {0} for interpreter not found",
                        Utility.FormatWrapOrNull(key));

                    return ReturnCode.Error;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified trusted key ring file name, identified by the
        /// specified hash value, for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that will own the key ring file name.
        /// </param>
        /// <param name="hashValue">
        /// The hash value identifying the key ring file.
        /// </param>
        /// <param name="fileName">
        /// The trusted key ring file name to add.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the per-interpreter file name collection when
        /// one does not already exist.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        public static ReturnCode AddFile(
            Interpreter interpreter, /* in */
            byte[] hashValue,        /* in */
            string fileName,         /* in */
            bool create              /* in */
            )
        {
            Result error = null;

            return AddFile(
                interpreter, hashValue, fileName, create, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Adds the specified trusted key ring file name, identified by the
        /// specified hash value, for the specified interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter that will own the key ring file name.
        /// </param>
        /// <param name="hashValue">
        /// The hash value identifying the key ring file.
        /// </param>
        /// <param name="fileName">
        /// The trusted key ring file name to add.
        /// </param>
        /// <param name="create">
        /// Non-zero to create the per-interpreter file name collection when
        /// one does not already exist.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success;
        /// <see cref="ReturnCode.Error" /> on failure.
        /// </returns>
        private static ReturnCode AddFile(
            Interpreter interpreter, /* in */
            byte[] hashValue,        /* in */
            string fileName,         /* in */
            bool create,             /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

            if (hashValue == null)
            {
                error = "invalid hash value";
                return ReturnCode.Error;
            }

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (fileNames == null)
                {
                    error = "key ring file names not available";
                    return ReturnCode.Error;
                }

                StringDictionary dictionary = null;
                object value;

                if (fileNames.TryGetValue(interpreter, out value))
                {
                    dictionary = value as StringDictionary;
                }
                else if (create)
                {
                    dictionary = new StringDictionary();
                    fileNames.Add(interpreter, dictionary);
                }

                if (dictionary == null)
                {
                    error = "no key ring file names for interpreter";
                    return ReturnCode.Error;
                }

                string key = CertificateDataOps.FormatHexadecimal(
                    hashValue, true);

                if (key == null)
                {
                    error = "invalid key from hash value";
                    return ReturnCode.Error;
                }

                dictionary[key] = fileName;
                return ReturnCode.Ok;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Cleanup Methods
        /// <summary>
        /// Counts the trusted key rings belonging to disposed interpreters,
        /// appending a summary to <paramref name="builder" /> and updating
        /// <paramref name="totalCount" />.
        /// </summary>
        /// <param name="priority">
        /// The trace priority to use when emitting diagnostic output.
        /// </param>
        /// <param name="builder">
        /// Receives a textual summary of the counted key rings; it is
        /// created when null and a non-zero count is found.
        /// </param>
        /// <param name="totalCount">
        /// Receives the running total of counted key rings.
        /// </param>
        public static void MaybeCountAll(
            TracePriority priority,    /* in */
            ref StringBuilder builder, /* in, out */
            ref int totalCount         /* in, out */
            ) /* CORE? */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyRings == null)
                    return;

                int count = 0;

                foreach (KeyRingPair pair in keyRings)
                {
                    Interpreter interpreter =
                        pair.Key as Interpreter;

                    if (interpreter == null)
                        continue;

                    if (!interpreter.Disposed)
                        continue;

                    KeyRingDictionary dictionary =
                        pair.Value as KeyRingDictionary;

                    if (dictionary != null)
                    {
#if DEBUG || FORCE_TRACE
                        DebugOnlyOps.DumpKeyRings(interpreter,
                            String.Format("MaybeCountAll({0})",
                            CertificateDataOps.FormatInterpreter(
                                interpreter, true, true)), dictionary,
                            typeof(CertificateKeyRingState).Name,
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
                        "trustedKeyRings(interpreters, {0})", count);

                    totalCount += count;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the trusted key rings belonging to disposed interpreters,
        /// appending a summary to <paramref name="builder" /> and updating
        /// <paramref name="totalCount" />.
        /// </summary>
        /// <param name="builder">
        /// Receives a textual summary of the cleaned up key rings; it is
        /// created when null and a non-zero count is found.
        /// </param>
        /// <param name="totalCount">
        /// Receives the running total of cleaned up key rings.
        /// </param>
        public static void MaybeCleanupAll(
            ref StringBuilder builder, /* in, out */
            ref int totalCount         /* in, out */
            ) /* CORE? */
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (keyRings == null)
                    return;

                int count = 0;

                InterpreterList keys = new InterpreterList(
                    keyRings.Keys);

                foreach (IInterpreter interpreter in keys)
                {
                    if (interpreter == null)
                        continue;

                    if (!interpreter.Disposed)
                        continue;

                    if (keyRings.Remove(interpreter))
                        count++;
                }

                if (count > 0)
                {
                    if (builder == null)
                        builder = new StringBuilder();

                    if (builder.Length > 0)
                        builder.Append(Characters.Space);

                    builder.AppendFormat(
                        "trustedKeyRings({0})", count);

                    totalCount += count;
                }
            }
        }
        #endregion
    }
}
