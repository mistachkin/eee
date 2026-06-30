/*
 * DictionaryOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Collections.Generic;
using Eagle._Attributes;
using Eagle._Components.Public;

namespace Badge.Components.Private
{
    /// <summary>
    /// Provides safe, error-reporting helper operations over a generic
    /// dictionary (list keys, get, set, remove, and clear), used to manage
    /// the Badge plugin's override-string collection.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the dictionary keys.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of the dictionary values.
    /// </typeparam>
    [ObjectId("a9faf2b6-b6be-4208-8741-b33a420e6534")]
    internal static class DictionaryOps<TKey, TValue>
    {
        /// <summary>
        /// Copies the keys of the dictionary into a new list.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary whose keys are listed.
        /// </param>
        /// <param name="list">
        /// Upon success, receives the list of keys.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool TryListKeys(
            IDictionary<TKey, TValue> dictionary, /* in */
            out List<TKey> list,                  /* out */
            ref Result error                      /* out */
            )
        {
            if (dictionary == null)
            {
                list = null;
                error = "invalid dictionary";

                return false;
            }

            ICollection<TKey> keys = dictionary.Keys;

            if (keys == null)
            {
                list = null;
                error = "dictionary has invalid keys collection";

                return false;
            }

            list = new List<TKey>(keys.Count);
            list.AddRange(keys);

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary to query.
        /// </param>
        /// <param name="key">
        /// The key whose value is requested.
        /// </param>
        /// <param name="value">
        /// Upon success, receives the value associated with the key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero when the key was present; otherwise, zero.
        /// </returns>
        public static bool TryGetValue(
            IDictionary<TKey, TValue> dictionary, /* in */
            TKey key,                             /* in */
            out TValue value,                     /* out */
            ref Result error                      /* out */
            )
        {
            if (dictionary == null)
            {
                value = default(TValue);
                error = "invalid dictionary";

                return false;
            }

            if (key == null)
            {
                value = default(TValue);
                error = "invalid key";

                return false;
            }

            if (dictionary.TryGetValue(key, out value))
                return true;

            error = "key not present";
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the value associated with the specified key.  When
        /// <paramref name="addOnly" /> is non-zero, an existing key is not
        /// overwritten and the operation fails; otherwise the value is added
        /// or updated.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary to modify.
        /// </param>
        /// <param name="key">
        /// The key to set.
        /// </param>
        /// <param name="value">
        /// The value to associate with the key.
        /// </param>
        /// <param name="addOnly">
        /// Non-zero to only add a new key (failing when it already exists);
        /// zero to add or update.
        /// </param>
        /// <param name="added">
        /// Upon return, non-zero when a new key was added (rather than an
        /// existing one updated).
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool TrySetValue(
            IDictionary<TKey, TValue> dictionary, /* in */
            TKey key,                             /* in */
            TValue value,                         /* in */
            bool addOnly,                         /* in */
            ref bool added,                       /* out */
            ref Result error                      /* out */
            )
        {
            if (dictionary == null)
            {
                error = "invalid dictionary";
                return false;
            }

            if (key == null)
            {
                error = "invalid key";
                return false;
            }

            bool contains = dictionary.ContainsKey(key);

            if (addOnly)
            {
                if (contains)
                {
                    error = "key already present";
                }
                else
                {
                    dictionary.Add(key, value);
                    added = true;
                }

                return !contains;
            }
            else
            {
                if (contains)
                {
                    dictionary[key] = value;
                    added = false;
                }
                else
                {
                    dictionary.Add(key, value);
                    added = true;
                }

                return true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the entry with the specified key from the dictionary.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary to modify.
        /// </param>
        /// <param name="key">
        /// The key to remove.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero when the key was present and removed; otherwise, zero.
        /// </returns>
        public static bool TryRemoveValue(
            IDictionary<TKey, TValue> dictionary, /* in */
            TKey key,                             /* in */
            ref Result error                      /* out */
            )
        {
            if (dictionary == null)
            {
                error = "invalid dictionary";
                return false;
            }

            if (key == null)
            {
                error = "invalid key";
                return false;
            }

            if (dictionary.Remove(key))
                return true;

            error = "key not present";
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes all entries from the dictionary, reporting how many were
        /// present beforehand.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary to clear.
        /// </param>
        /// <param name="count">
        /// Upon success, receives the number of entries that were removed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public static bool TryClear(
            IDictionary<TKey, TValue> dictionary, /* in */
            ref int count,                        /* out */
            ref Result error                      /* out */
            )
        {
            if (dictionary == null)
            {
                error = "invalid dictionary";
                return false;
            }

            count = dictionary.Count;
            dictionary.Clear();

            return true;
        }
    }
}
