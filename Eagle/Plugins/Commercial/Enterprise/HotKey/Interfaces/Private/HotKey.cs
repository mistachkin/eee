/*
 * HotKey.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
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
    /// Represents a single global hot-key: its owning form and window handle,
    /// its key combination, descriptive text, registration and hit state, and
    /// the script (and captured result/error information) associated with its
    /// activation.
    /// </summary>
    [ObjectId("0f341421-020d-456a-94d9-523c29468c9b")]
    internal interface IHotKey : IMaybeDisposed
    {
        /// <summary>
        /// Gets the object used to synchronize access to this hot-key.
        /// </summary>
        object SyncRoot { get; }

        /// <summary>
        /// Gets the form that owns this hot-key.
        /// </summary>
        Form Form { get; }

        /// <summary>
        /// Gets the window handle this hot-key is registered against.
        /// </summary>
        IntPtr Handle { get; }

        /// <summary>
        /// Gets the integer id that identifies this hot-key.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the full key combination (modifiers plus virtual key).
        /// </summary>
        Keys Keys { get; }

        /// <summary>
        /// Gets the modifier keys portion of the key combination.
        /// </summary>
        Keys Modifiers { get; }

        /// <summary>
        /// Gets the virtual (non-modifier) key portion of the key
        /// combination.
        /// </summary>
        Keys VirtualKey { get; }

        /// <summary>
        /// Gets the flags that control this hot-key's behavior.
        /// </summary>
        HotKeyFlags Flags { get; }

        /// <summary>
        /// Gets the descriptive text associated with this hot-key.
        /// </summary>
        string Text { get; }

        /// <summary>
        /// Gets a value indicating whether this hot-key is currently
        /// registered with the operating system.
        /// </summary>
        bool Registered { get; }

        /// <summary>
        /// Gets the number of times this hot-key has been activated.
        /// </summary>
        int HitCount { get; }

        /// <summary>
        /// Gets the time of the most recent activation, or null if it has
        /// never been activated.
        /// </summary>
        DateTime? HitTime { get; }

        /// <summary>
        /// Gets the script flags in effect at the most recent activation.
        /// </summary>
        HotKeyScriptFlags HitFlags { get; }

        /// <summary>
        /// Gets the return code captured from the most recent script
        /// evaluation.
        /// </summary>
        ReturnCode ReturnCode { get; }

        /// <summary>
        /// Gets the result captured from the most recent script evaluation.
        /// </summary>
        Result Result { get; }

        /// <summary>
        /// Gets the error line number captured from the most recent script
        /// evaluation.
        /// </summary>
        int ErrorLine { get; }

        /// <summary>
        /// Gets the error code captured from the most recent script
        /// evaluation.
        /// </summary>
        Result ErrorCode { get; }

        /// <summary>
        /// Gets the error information captured from the most recent script
        /// evaluation.
        /// </summary>
        Result ErrorInfo { get; }

        /// <summary>
        /// Notifies this hot-key that its owning form and window handle have
        /// changed (for example, after the manager window is recreated).
        /// </summary>
        /// <param name="form">
        /// The new owning form.
        /// </param>
        /// <param name="handle">
        /// The new window handle.
        /// </param>
        void ParentHasChanged(Form form, IntPtr handle);

        /// <summary>
        /// Gets a display tag for this hot-key, qualified by the supplied
        /// name.
        /// </summary>
        /// <param name="name">
        /// The name used to qualify the display tag.
        /// </param>
        /// <returns>
        /// The display tag.
        /// </returns>
        string GetDisplayTag(string name);

        /// <summary>
        /// Produces a list representation of this hot-key.
        /// </summary>
        /// <param name="full">
        /// Non-zero to include all fields; zero for the summary set.
        /// </param>
        /// <returns>
        /// A list describing this hot-key.
        /// </returns>
        StringList ToList(bool full);

        /// <summary>
        /// Produces a list representation of this hot-key, selecting which
        /// groups of fields to include.
        /// </summary>
        /// <param name="flagsMask">
        /// The mask limiting which flags are reported.
        /// </param>
        /// <param name="manager">
        /// Non-zero to include manager-related fields.
        /// </param>
        /// <param name="script">
        /// Non-zero to include script-related fields.
        /// </param>
        /// <param name="other">
        /// Non-zero to include other (miscellaneous) fields.
        /// </param>
        /// <param name="results">
        /// Non-zero to include captured result fields.
        /// </param>
        /// <returns>
        /// A list describing the selected fields of this hot-key.
        /// </returns>
        StringList ToList(HotKeyFlags flagsMask, bool manager, bool script,
            bool other, bool results);

        /// <summary>
        /// Determines whether this hot-key has the specified flags.
        /// </summary>
        /// <param name="hasFlags">
        /// The flags to test for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the flags; zero to require any.
        /// </param>
        /// <returns>
        /// Non-zero when the flags are present; otherwise, zero.
        /// </returns>
        bool HasFlags(HotKeyFlags hasFlags, bool all);

        /// <summary>
        /// Clears the record that this hot-key was previously registered.
        /// </summary>
        void ClearWasRegistered();

        /// <summary>
        /// Records that this hot-key was previously registered.
        /// </summary>
        void SetWasRegistered();

        /// <summary>
        /// Registers this hot-key with the operating system so it becomes
        /// active.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode Register(ref Result error);

        /// <summary>
        /// Unregisters this hot-key from the operating system so it is no
        /// longer active.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        ReturnCode Unregister(ref Result error);

        /// <summary>
        /// Clears the captured return code, result, and error information from
        /// the most recent script evaluation.
        /// </summary>
        void ResetResult();

        /// <summary>
        /// Evaluates this hot-key's associated script, capturing its outcome.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which to evaluate the script.
        /// </param>
        /// <param name="flags">
        /// The script flags controlling how the script is evaluated.
        /// </param>
        void EvaluateScript(Interpreter interpreter, HotKeyScriptFlags flags);

        /// <summary>
        /// Sets the captured error code and error information for this
        /// hot-key.
        /// </summary>
        /// <param name="errorCode">
        /// The error code to record.
        /// </param>
        /// <param name="errorInfo">
        /// The error information to record.
        /// </param>
        void SetErrorCodeAndInfo(Result errorCode, Result errorInfo);

        /// <summary>
        /// Copies this hot-key's captured return code and result (and error
        /// state) into the specified interpreter and the supplied reference
        /// arguments.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to copy the result into.
        /// </param>
        /// <param name="returnCode">
        /// On output, receives the captured return code.
        /// </param>
        /// <param name="result">
        /// On output, receives the captured result.
        /// </param>
        void ResultToInterpreter(Interpreter interpreter,
            ref ReturnCode returnCode, ref Result result);
    }
}
