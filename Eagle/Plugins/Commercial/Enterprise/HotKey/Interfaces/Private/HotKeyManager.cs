/*
 * HotKeyManager.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Globalization;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using HotKey.Components.Private;

namespace HotKey.Interfaces.Private
{
    //
    // NOTE: This interface is currently private; however, it may be "promoted"
    //       to public at some point.
    //
    /// <summary>
    /// Represents the hot-key manager: the owner of the hidden hot-key window
    /// and the collection of defined hot-keys.  It provides the operations to
    /// count, list, find, get, set, add, remove, load, and save hot-keys, as
    /// well as the hook scripts and event data used during hot-key
    /// processing.
    /// </summary>
    [ObjectId("03254d69-fb6a-441c-9fe6-46f687386002")]
    internal interface IHotKeyManager
    {
        /// <summary>
        /// Gets a value indicating whether the manager has been closed.
        /// </summary>
        bool IsClosed { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the manager emits
        /// notifications for hot-key events.
        /// </summary>
        bool Notify { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the manager is logging.
        /// </summary>
        bool Logging { get; set; }

        /// <summary>
        /// Gets or sets the title of the manager form.
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// Gets the manager's form.
        /// </summary>
        /// <returns>
        /// The hot-key manager form.
        /// </returns>
        Form GetHotKeyManagerForm();

        /// <summary>
        /// Gets the window handle that hot-keys are registered against.
        /// </summary>
        /// <returns>
        /// The hot-key window handle.
        /// </returns>
        IntPtr GetHotKeyHandle();

        /// <summary>
        /// Gets the next available hot-key id.
        /// </summary>
        /// <returns>
        /// A new, unused hot-key id.
        /// </returns>
        int GetNextHotKeyId();

        /// <summary>
        /// Gets the hook script associated with the specified hook type.
        /// </summary>
        /// <param name="type">
        /// The hook type whose script is requested.
        /// </param>
        /// <param name="text">
        /// On output, receives the hook script text.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode GetHookScriptFor(
            HotKeyHookType type, ref string text, ref Result error);

        /// <summary>
        /// Sets the hook script associated with the specified hook type.
        /// </summary>
        /// <param name="type">
        /// The hook type whose script is being set.
        /// </param>
        /// <param name="text">
        /// On input, the hook script text to set; on output, may receive the
        /// stored value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode SetHookScriptFor(
            HotKeyHookType type, ref string text, ref Result error);

        /// <summary>
        /// Gets data describing the previous hot-key event processed by the
        /// manager.
        /// </summary>
        /// <param name="result">
        /// On output, receives the previous event data, or an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode GetPreviousEventData(ref Result result);

        /// <summary>
        /// Counts the defined hot-keys.
        /// </summary>
        /// <param name="registered">
        /// Non-zero to count only currently registered hot-keys.
        /// </param>
        /// <param name="count">
        /// On output, receives the count.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode CountHotKeys(bool registered, ref int count,
            ref Result error);

        /// <summary>
        /// Clears the defined hot-keys.
        /// </summary>
        /// <param name="unregisterOnly">
        /// Non-zero to only unregister the hot-keys, keeping their
        /// definitions.
        /// </param>
        /// <param name="force">
        /// Non-zero to clear even when normally disallowed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode ClearHotKeys(bool unregisterOnly, bool force,
            ref Result error);

        /// <summary>
        /// Lists the ids of all defined hot-keys.
        /// </summary>
        /// <param name="ids">
        /// On output, receives the list of hot-key ids.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode ListHotKeys(ref IntList ids, ref Result error);

        /// <summary>
        /// Finds the ids of hot-keys matching the specified key combination.
        /// </summary>
        /// <param name="keys">
        /// The key combination to match.
        /// </param>
        /// <param name="exact">
        /// Non-zero to require an exact match.
        /// </param>
        /// <param name="all">
        /// Non-zero to return all matches; zero to return only the first.
        /// </param>
        /// <param name="ids">
        /// On output, receives the matching hot-key ids.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode FindHotKeys(Keys keys, bool exact, bool all,
            ref IntList ids, ref Result error);

        /// <summary>
        /// Finds the ids of hot-keys matching the specified flags.
        /// </summary>
        /// <param name="flags">
        /// The flags to match.
        /// </param>
        /// <param name="exact">
        /// Non-zero to require an exact match.
        /// </param>
        /// <param name="all">
        /// Non-zero to return all matches; zero to return only the first.
        /// </param>
        /// <param name="ids">
        /// On output, receives the matching hot-key ids.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode FindHotKeys(HotKeyFlags flags, bool exact, bool all,
            ref IntList ids, ref Result error);

        /// <summary>
        /// Finds the ids of hot-keys by registration state.
        /// </summary>
        /// <param name="registered">
        /// The registration state to match.
        /// </param>
        /// <param name="all">
        /// Non-zero to return all matches; zero to return only the first.
        /// </param>
        /// <param name="ids">
        /// On output, receives the matching hot-key ids.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode FindHotKeys(bool registered, bool all, ref IntList ids,
            ref Result error);

        /// <summary>
        /// Gets the hot-key identified by a value resolvable to a hot-key id.
        /// </summary>
        /// <param name="getValue">
        /// The value identifying the hot-key.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when resolving the value.
        /// </param>
        /// <param name="hotKey">
        /// On output, receives the resolved hot-key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode GetHotKey(IGetValue getValue, CultureInfo cultureInfo,
            ref IHotKey hotKey, ref Result error);

        /// <summary>
        /// Gets the hot-key with the specified id.
        /// </summary>
        /// <param name="id">
        /// The id of the hot-key to get.
        /// </param>
        /// <param name="hotKey">
        /// On output, receives the hot-key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode GetHotKey(int id, ref IHotKey hotKey, ref Result error);

        /// <summary>
        /// Sets (replaces) the hot-key identified by a value resolvable to a
        /// hot-key id.
        /// </summary>
        /// <param name="getValue">
        /// The value identifying the hot-key to replace.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when resolving the value.
        /// </param>
        /// <param name="hotKey">
        /// The replacement hot-key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode SetHotKey(IGetValue getValue, CultureInfo cultureInfo,
            IHotKey hotKey, ref Result error);

        /// <summary>
        /// Sets (replaces) the hot-key with the specified id.
        /// </summary>
        /// <param name="id">
        /// The id of the hot-key to replace.
        /// </param>
        /// <param name="hotKey">
        /// The replacement hot-key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode SetHotKey(int id, IHotKey hotKey, ref Result error);

        /// <summary>
        /// Adds an existing hot-key instance to the manager.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key to add.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode AddHotKey(IHotKey hotKey, ref Result error);

        /// <summary>
        /// Creates and adds a new hot-key for the specified key combination,
        /// flags, and text.
        /// </summary>
        /// <param name="keys">
        /// The key combination for the new hot-key.
        /// </param>
        /// <param name="flags">
        /// The flags for the new hot-key.
        /// </param>
        /// <param name="text">
        /// The descriptive text for the new hot-key.
        /// </param>
        /// <param name="id">
        /// On output, receives the id assigned to the new hot-key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode AddHotKey(Keys keys, HotKeyFlags flags, string text,
            ref int id, ref Result error);

        /// <summary>
        /// Removes the hot-key identified by a value resolvable to a hot-key
        /// id.
        /// </summary>
        /// <param name="getValue">
        /// The value identifying the hot-key to remove.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when resolving the value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode RemoveHotKey(IGetValue getValue, CultureInfo cultureInfo,
            ref Result error);

        /// <summary>
        /// Removes the hot-key with the specified id.
        /// </summary>
        /// <param name="id">
        /// The id of the hot-key to remove.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode RemoveHotKey(int id, ref Result error);

        /// <summary>
        /// Loads hot-key definitions from the supplied text.
        /// </summary>
        /// <param name="text">
        /// The text containing the hot-key definitions to load.
        /// </param>
        /// <param name="strictCount">
        /// Non-zero to require the expected number of hot-keys.
        /// </param>
        /// <param name="strictRegister">
        /// Non-zero to require each loaded hot-key to register successfully.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode LoadHotKeys(string text, bool strictCount,
            bool strictRegister, ref Result error);

        /// <summary>
        /// Serializes the current hot-key definitions to text.
        /// </summary>
        /// <param name="strict">
        /// Non-zero to enforce stricter serialization rules.
        /// </param>
        /// <param name="text">
        /// On output, receives the serialized hot-key text.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode SaveHotKeys(bool strict, ref string text, ref Result error);
    }
}
