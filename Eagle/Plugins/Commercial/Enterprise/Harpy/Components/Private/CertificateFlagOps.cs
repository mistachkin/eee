/*
 * CertificateFlagOps.cs --
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

using FlagsRules = System.Collections.Generic.List<
    System.Collections.Generic.IEnumerable<string>>;

using FlagsPair = System.Collections.Generic.KeyValuePair<long, string>;
using FlagsDictionary = System.Collections.Generic.IDictionary<long, string>;

using SortedFlagsDictionary = System.Collections.Generic.SortedDictionary<
    ulong, string>;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides static helper methods for changing, querying, and checking
    /// the attribute flags associated with a certificate.
    /// </summary>
    [ObjectId("d41b9e94-19e1-4837-b34d-7cae3d6faa6b")]
    internal static class CertificateFlagOps
    {
        /// <summary>
        /// Applies a simple change to the default attribute flags contained
        /// in <paramref name="oldText" />, producing the updated set of flags
        /// in <paramref name="newText" />.
        /// </summary>
        /// <param name="oldText">
        /// The existing attribute flags, as a string, to be changed.  A null
        /// value is treated as an empty string.
        /// </param>
        /// <param name="changeText">
        /// The change to apply to the default attribute flags.
        /// </param>
        /// <param name="newText">
        /// Upon success, receives the resulting attribute flags as a string,
        /// or null when the resulting set of flags is empty.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode SimpleChange( /* CORE */
            string oldText,     /* in */
            string changeText,  /* in */
            ref string newText, /* out */
            ref Result error    /* out */
            )
        {
            if (oldText == null)
                oldText = String.Empty;

            FlagsDictionary flags = Utility.ParseAttributeFlags(
                oldText, true, false, false, ref error);

            if (flags == null)
                return ReturnCode.Error;

            long key = Utility.DefaultAttributeFlagsKey();

            flags = Utility.ChangeAttributeFlags(
                flags, key, changeText, false, ref error);

            if (flags == null)
                return ReturnCode.Error;

            string localNewText = Utility.FormatAttributeFlags(
                flags, false, false, false, false, ref error);

            if (localNewText == null)
                return ReturnCode.Error;

            //
            // HACK: If the flags are a zero-length string at this
            //       point, change them to null.  This permits the
            //       (associated?) internal state to be completely
            //       reset.
            //
            if (localNewText.Length == 0)
                localNewText = null;

            newText = localNewText;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies a change to the attribute flags contained in
        /// <paramref name="oldText" /> for the specified key, producing the
        /// updated set of flags in <paramref name="newText" />.
        /// </summary>
        /// <param name="oldText">
        /// The existing attribute flags, as a string, to be changed.
        /// </param>
        /// <param name="changeText">
        /// The change to apply to the attribute flags.
        /// </param>
        /// <param name="key">
        /// The key identifying the set of attribute flags to change.  A
        /// non-default key requires <paramref name="complex" /> mode.
        /// </param>
        /// <param name="complex">
        /// Non-zero to enable complex mode, which permits the use of
        /// non-default keys.
        /// </param>
        /// <param name="legacy">
        /// Non-zero to format the resulting flags using the legacy format.
        /// </param>
        /// <param name="compact">
        /// Non-zero to format the resulting flags using a compact
        /// representation.
        /// </param>
        /// <param name="space">
        /// Non-zero to allow and emit spaces when parsing and formatting the
        /// flags.
        /// </param>
        /// <param name="sort">
        /// Non-zero to sort the resulting flags.
        /// </param>
        /// <param name="newText">
        /// Upon success, receives the resulting attribute flags as a string,
        /// or null when the resulting set of flags is empty.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Change( /* CORE */
            string oldText,     /* in */
            string changeText,  /* in */
            long key,           /* in */
            bool complex,       /* in */
            bool legacy,        /* in */
            bool compact,       /* in */
            bool space,         /* in */
            bool sort,          /* in */
            ref string newText, /* out */
            ref Result error    /* out */
            )
        {
            if (!complex &&
                (key != Utility.DefaultAttributeFlagsKey()))
            {
                error = String.Format(
                    "must use complex mode to use non-default key {0}",
                    key);

                return ReturnCode.Error;
            }

            Result localError; /* REUSED */
            FlagsDictionary oldFlags;

            localError = null;

            oldFlags = Utility.ParseAttributeFlags(
                oldText, complex, space, sort, ref localError);

            if (oldFlags == null)
            {
                error = String.Format(
                    "invalid \"old\" flags: {0}", localError);

                return ReturnCode.Error;
            }

            localError = null;

            oldFlags = Utility.ChangeAttributeFlags(
                oldFlags, key, changeText, sort, ref localError);

            if (oldFlags == null)
            {
                error = String.Format(
                    "invalid \"change\" flags: {0}", localError);

                return ReturnCode.Error;
            }

            localError = null;

            string localNewText = Utility.FormatAttributeFlags(
                oldFlags, legacy, compact, space, sort,
                ref localError);

            if (localNewText == null)
            {
                error = localError;
                return ReturnCode.Error;
            }

            //
            // HACK: If the flags are a zero-length string at this
            //       point, change them to null.  This permits the
            //       (associated?) internal state to be completely
            //       reset.
            //
            if (localNewText.Length == 0)
                localNewText = null;

            newText = localNewText;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

#if CERTIFICATE_PLUGIN && PLUGIN_COMMANDS
        /// <summary>
        /// Creates a copy of the specified attribute flags as a dictionary
        /// sorted by key.
        /// </summary>
        /// <param name="flags">
        /// The attribute flags to copy and sort.  A null value results in a
        /// null return value.
        /// </param>
        /// <returns>
        /// A new dictionary containing the specified flags sorted by key, or
        /// null if <paramref name="flags" /> is null.
        /// </returns>
        public static SortedFlagsDictionary GetSorted( /* CORE */
            FlagsDictionary flags /* in */
            )
        {
            if (flags == null)
                return null;

            SortedFlagsDictionary result = new SortedFlagsDictionary();

            foreach (FlagsPair pair in flags)
                result[(ulong)pair.Key] = pair.Value;

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the attribute flags contained in
        /// <paramref name="oldText" /> include those specified in
        /// <paramref name="haveText" /> for the given key.
        /// </summary>
        /// <param name="oldText">
        /// The existing attribute flags, as a string, to be examined.
        /// </param>
        /// <param name="haveText">
        /// The attribute flags, as a string, to look for.
        /// </param>
        /// <param name="key">
        /// The key identifying the set of attribute flags to examine.  A
        /// non-default key requires <paramref name="complex" /> mode.
        /// </param>
        /// <param name="complex">
        /// Non-zero to enable complex mode, which permits the use of
        /// non-default keys.
        /// </param>
        /// <param name="space">
        /// Non-zero to allow spaces when parsing the flags.
        /// </param>
        /// <param name="sort">
        /// Non-zero to sort the parsed flags.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the specified flags be present.
        /// </param>
        /// <param name="strict">
        /// Non-zero to use strict matching semantics.
        /// </param>
        /// <param name="result">
        /// Upon success, receives non-zero if the specified flags are present;
        /// otherwise, zero.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Have(
            string oldText,   /* in */
            string haveText,  /* in */
            long key,         /* in */
            bool complex,     /* in */
            bool space,       /* in */
            bool sort,        /* in */
            bool all,         /* in */
            bool strict,      /* in */
            ref bool? result, /* out */
            ref Result error  /* out */
            )
        {
            if (!complex &&
                (key != Utility.DefaultAttributeFlagsKey()))
            {
                error = String.Format(
                    "must use complex mode to use non-default key {0}",
                    key);

                return ReturnCode.Error;
            }

            Result localError; /* REUSED */
            FlagsDictionary oldFlags;

            localError = null;

            oldFlags = Utility.ParseAttributeFlags(
                oldText, complex, space, sort, ref localError);

            if (oldFlags == null)
            {
                error = String.Format(
                    "invalid \"old\" flags: {0}", localError);

                return ReturnCode.Error;
            }

            localError = null;

            if (!Utility.VerifyAttributeFlags(
                    haveText, false, space, ref localError))
            {
                error = String.Format(
                    "invalid \"have\" flags: {0}", localError);

                return ReturnCode.Error;
            }

            result = Utility.HaveAttributeFlags(
                oldFlags, key, haveText, all, strict);

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Checks the attribute flags contained in <paramref name="oldText" />
        /// against a set of allow and deny rules, evaluated in the order
        /// indicated by <paramref name="ruleType" />.
        /// </summary>
        /// <param name="oldText">
        /// The existing attribute flags, as a string, to be checked.
        /// </param>
        /// <param name="allowRules">
        /// The collection of rules whose flags are permitted.
        /// </param>
        /// <param name="denyRules">
        /// The collection of rules whose flags are forbidden.
        /// </param>
        /// <param name="ruleType">
        /// Flags controlling the rule set ordering as well as the key and rule
        /// matching semantics used during the check.
        /// </param>
        /// <param name="complex">
        /// Non-zero to enable complex mode when parsing the flags.
        /// </param>
        /// <param name="space">
        /// Non-zero to allow spaces when parsing the flags.
        /// </param>
        /// <param name="sort">
        /// Non-zero to sort the parsed flags.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the specified flags be present.
        /// </param>
        /// <param name="strict">
        /// Non-zero to use strict matching semantics.
        /// </param>
        /// <param name="result">
        /// Upon success, receives non-zero if the flags satisfy the configured
        /// rules; otherwise, zero.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode Check(
            string oldText,                 /* in */
            IEnumerable<string> allowRules, /* in */
            IEnumerable<string> denyRules,  /* in */
            FlagRuleType ruleType,          /* in */
            bool complex,                   /* in */
            bool space,                     /* in */
            bool sort,                      /* in */
            bool all,                       /* in */
            bool strict,                    /* in */
            ref bool? result,               /* out */
            ref Result error                /* out */
            )
        {
            Result localError; /* REUSED */
            FlagsDictionary oldFlags;

            localError = null;

            oldFlags = Utility.ParseAttributeFlags(
                oldText, complex, space, sort, ref localError);

            if (oldFlags == null)
            {
                error = String.Format(
                    "invalid \"old\" flags: {0}", localError);

                return ReturnCode.Error;
            }

            bool[] oldResults;
            FlagsRules rules;

            if (CertificateSharedOps.HasFlags(
                    ruleType, FlagRuleType.AllowDeny, true))
            {
                oldResults = new bool[] { true, false };

                rules = new FlagsRules();
                rules.Add(allowRules);
                rules.Add(denyRules);
            }
            else if (CertificateSharedOps.HasFlags(
                    ruleType, FlagRuleType.DenyAllow, true))
            {
                oldResults = new bool[] { false, true };

                rules = new FlagsRules();
                rules.Add(denyRules);
                rules.Add(allowRules);
            }
            else
            {
                error = String.Format(
                    "unsupported rule set ordering {0}",
                    Utility.FormatWrapOrNull(ruleType));

                return ReturnCode.Error;
            }

            bool matchAllKey;

            if (CertificateSharedOps.HasFlags(
                    ruleType, FlagRuleType.MatchAllKey, true))
            {
                matchAllKey = true;
            }
            else if (CertificateSharedOps.HasFlags(
                    ruleType, FlagRuleType.MatchAnyKey, true))
            {
                matchAllKey = false;
            }
            else
            {
                error = String.Format(
                    "unsupported key matching {0}",
                    Utility.FormatWrapOrNull(ruleType));

                return ReturnCode.Error;
            }

            bool matchAllRule;

            if (CertificateSharedOps.HasFlags(
                    ruleType, FlagRuleType.MatchAllRule, true))
            {
                matchAllRule = true;
            }
            else if (CertificateSharedOps.HasFlags(
                    ruleType, FlagRuleType.MatchAnyRule, true))
            {
                matchAllRule = false;
            }
            else
            {
                error = String.Format(
                    "unsupported rule matching {0}",
                    Utility.FormatWrapOrNull(ruleType));

                return ReturnCode.Error;
            }

            int count = rules.Count;

            if ((oldResults == null) ||
                (oldResults.Length != count))
            {
                error = String.Format(
                    "mismatched rule counts: {0} versus {1}",
                    (oldResults != null) ? oldResults.Length :
                    Length.Invalid, count);

                return ReturnCode.Error;
            }

            IEnumerable<string> rule; /* REUSED */
            FlagsDictionary flags; /* REUSED */
            bool newResult; /* REUSED */

            if (matchAllRule)
            {
                for (int index = 0; index < count; index++)
                {
                    rule = rules[index];

                    if (rule == null)
                        continue;

                    int checkCount = 0;
                    int matchCount = 0;

                    foreach (string text in rule)
                    {
                        if (text == null)
                            continue;

                        flags = Utility.ParseAttributeFlags(
                            text, complex, space, sort,
                            ref localError);

                        if (matchAllKey)
                        {
                            newResult = false;

                            if (MatchAll(
                                    oldFlags, flags, all,
                                    strict, ref newResult,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            checkCount++;

                            if (newResult)
                                matchCount++;
                        }
                        else
                        {
                            newResult = false;

                            if (MatchAny(
                                    oldFlags, flags, all,
                                    strict, ref newResult,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            checkCount++;

                            if (newResult)
                                matchCount++;
                        }
                    }

                    if (matchCount != checkCount)
                    {
                        result = false;
                        return ReturnCode.Ok;
                    }
                }
            }
            else
            {
                for (int index = 0; index < count; index++)
                {
                    rule = rules[index];

                    if (rule == null)
                        continue;

                    bool oldResult = oldResults[index];

                    newResult = false;

                    foreach (string text in rule)
                    {
                        if (text == null)
                            continue;

                        flags = Utility.ParseAttributeFlags(
                            text, complex, space, sort,
                            ref localError);

                        if (matchAllKey)
                        {
                            if (MatchAll(
                                    oldFlags, flags, all,
                                    strict, ref newResult,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }
                        }
                        else
                        {
                            if (MatchAny(
                                    oldFlags, flags, all,
                                    strict, ref newResult,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }
                        }

                        if (newResult)
                            break;
                    }

                    if (newResult != oldResult)
                    {
                        result = false;
                        return ReturnCode.Ok;
                    }
                }
            }

            result = true;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether any of the flags in
        /// <paramref name="haveFlags" /> are present in
        /// <paramref name="oldFlags" />.
        /// </summary>
        /// <param name="oldFlags">
        /// The existing attribute flags to be examined.
        /// </param>
        /// <param name="haveFlags">
        /// The attribute flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the specified flags be present.
        /// </param>
        /// <param name="strict">
        /// Non-zero to use strict matching semantics.
        /// </param>
        /// <param name="result">
        /// Upon success, receives non-zero if at least one of the specified
        /// flags is present; otherwise, zero.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode MatchAny(
            FlagsDictionary oldFlags,  /* in */
            FlagsDictionary haveFlags, /* in */
            bool all,                  /* in */
            bool strict,               /* in */
            ref bool result,           /* out */
            ref Result error           /* out */
            )
        {
            if (oldFlags == null)
            {
                error = "invalid \"old\" flags";
                return ReturnCode.Error;
            }

            if (haveFlags == null)
            {
                error = "invalid \"have\" flags";
                return ReturnCode.Error;
            }

            foreach (KeyValuePair<long, string> pair in haveFlags)
            {
                if (Utility.HaveAttributeFlags(
                        oldFlags, pair.Key, pair.Value, all,
                        strict))
                {
                    result = true;
                    return ReturnCode.Ok;
                }
            }

            result = false;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether all of the flags in
        /// <paramref name="haveFlags" /> are present in
        /// <paramref name="oldFlags" />.
        /// </summary>
        /// <param name="oldFlags">
        /// The existing attribute flags to be examined.
        /// </param>
        /// <param name="haveFlags">
        /// The attribute flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require that all of the specified flags be present.
        /// </param>
        /// <param name="strict">
        /// Non-zero to use strict matching semantics.
        /// </param>
        /// <param name="result">
        /// Upon success, receives non-zero if all of the specified flags are
        /// present; otherwise, zero.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        private static ReturnCode MatchAll(
            FlagsDictionary oldFlags,  /* in */
            FlagsDictionary haveFlags, /* in */
            bool all,                  /* in */
            bool strict,               /* in */
            ref bool result,           /* out */
            ref Result error           /* out */
            )
        {
            if (oldFlags == null)
            {
                error = "invalid \"old\" flags";
                return ReturnCode.Error;
            }

            if (haveFlags == null)
            {
                error = "invalid \"have\" flags";
                return ReturnCode.Error;
            }

            int count = 0;

            foreach (KeyValuePair<long, string> pair in haveFlags)
            {
                if (Utility.HaveAttributeFlags(
                        oldFlags, pair.Key, pair.Value, all,
                        strict))
                {
                    count++;
                }
            }

            result = (count == haveFlags.Count);
            return ReturnCode.Ok;
        }
#endif
    }
}
