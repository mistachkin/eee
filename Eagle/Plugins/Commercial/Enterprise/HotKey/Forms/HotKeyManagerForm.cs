/*
 * HotKeyManagerForm.cs --
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
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using HotKey.Components.Private;
using HotKey.Interfaces.Private;

namespace HotKey.Forms
{
    #region Hot-Key Windows Forms Class
    /// <summary>
    /// Implements the hidden hot-key manager form: the window that registers
    /// global hot-keys, receives their activation messages, dispatches their
    /// scripts, and maintains the collection of hot-keys and hook scripts.  It
    /// realizes the manager, safe-result, and safe-close interfaces.
    /// </summary>
    [ObjectId("7453cde9-3bb1-4bce-9eb5-06b658da3972")]
    internal sealed partial class HotKeyManagerForm :
            BaseForm, IHotKeyManager, ISafeResult
    {
        #region Private Constants
        //
        // NOTE: This is the name of an optional script variable that can be
        //       used to cause this form to start minimized.
        //
        /// <summary>
        /// The name of the variable consulted to decide whether the form
        /// starts minimized.
        /// </summary>
        private static readonly string MinimizedVariableName =
            typeof(HotKeyManagerForm).FullName + "_Minimized";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum number of milliseconds to wait for the
        //       interpreter lock in order to check for the global variable
        //       that controls whether this form should minimize on startup.
        //
        /// <summary>
        /// The timeout used when reading the minimized variable.
        /// </summary>
        private const int MinimizedVariableTimeout = 5000;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prompt text shown when closing the manager with unsaved
        /// changes.
        /// </summary>
        private const string ClosingQuestionText =
            "Closing the hot-key manager will disable all {0} registered " +
            "hot-keys, continue anyhow?";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The newline sequence used when formatting saved hot-key text.
        /// </summary>
        private static readonly char NewLine = Characters.LineFeed;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The header written at the top of saved hot-key files.
        /// </summary>
        private static readonly string FileHeader =
            "###############################################################################" +
            NewLine + "#" + NewLine +
            "# This file contains a top-level Eagle script to re-add all the hot-keys saved" +
            NewLine +
            "# within it." + NewLine + "#" + NewLine +
            "# It can be loaded via the user-interface by clicking on the \"Load\" button." +
            NewLine + "#" + NewLine +
            "# It can also be loaded by using the Eagle script command [source] with one" +
            NewLine + "# argument, the full path and file name of this file." +
            NewLine + "#" + NewLine + "# Last saved on {0} by {1}\\{2}." + NewLine + "#" +
            NewLine +
            "###############################################################################" +
            NewLine;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The object used to synchronize access to the manager's state.
        /// </summary>
        private readonly object syncRoot = new object();
        /// <summary>
        /// The collection of defined hot-keys, keyed by id.
        /// </summary>
        private Dictionary<int, IHotKey> hotKeys;
        /// <summary>
        /// The installed hook scripts, keyed by hook type.
        /// </summary>
        private Dictionary<HotKeyHookType, string> hooks;
        /// <summary>
        /// The next hot-key id to assign.
        /// </summary>
        private int nextId;

        ///////////////////////////////////////////////////////////////////////

        #region Previous Event Data
        #region Lock-Free Data
        /// <summary>
        /// The key-down event arguments from the previous key event.
        /// </summary>
        private KeyEventArgs previousKeyDownEventArgs;
        /// <summary>
        /// The key-up event arguments from the previous key event.
        /// </summary>
        private KeyEventArgs previousKeyUpEventArgs;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Synchronized Data
#if NATIVE && WINDOWS
        /// <summary>
        /// The hot-key activated by the previous event.
        /// </summary>
        private IHotKey previousHotKey;
#endif
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Logically Read-Only Data
        /// <summary>
        /// The interpreter associated with the manager.
        /// </summary>
        private readonly Interpreter interpreter;
        /// <summary>
        /// The wait handle signaled when the manager has started.
        /// </summary>
        private readonly EventWaitHandle @event;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Synchronized Data
        /// <summary>
        /// Non-zero when the manager emits notifications for hot-key events.
        /// </summary>
        private bool notify;
        /// <summary>
        /// Non-zero when the manager is logging.
        /// </summary>
        private bool logging;
        /// <summary>
        /// The window handle that hot-keys are registered against.
        /// </summary>
        private IntPtr handle;
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new <see cref="HotKeyManagerForm" /> with the
        /// specified id, interpreter, result variable name, and startup event.
        /// </summary>
        /// <param name="id">
        /// The form id.
        /// </param>
        /// <param name="interpreter">
        /// The associated interpreter.
        /// </param>
        /// <param name="varName">
        /// The variable that receives the form id, if any.
        /// </param>
        /// <param name="event">
        /// The wait handle to signal once the manager has started.
        /// </param>
        public HotKeyManagerForm(
            int id,                  /* in */
            Interpreter interpreter, /* in */
            string varName,          /* in */
            EventWaitHandle @event   /* in */
            )
            : base(id, interpreter, varName)
        {
            //
            // NOTE: Call the automatically generated code used to initialize
            //       the Windows Forms properties of this object.
            //
            InitializeComponent();

            //
            // NOTE: The native Win32 window handle must be cached in a local
            //       field of this class for use by other threads (i.e. those
            //       that require it to create new hot-keys, etc).
            //
            /* NO RESULT */
            CacheHandle();

            //
            // NOTE: Save the Eagle interpreter to be used for all script
            //       evaluation and error reporting.
            //
            this.interpreter = interpreter;

            //
            // NOTE: Save the event wait handle for later so that we can
            //       signal it.
            //
            this.@event = @event;

            //
            // NOTE: Create the collection of hot-keys.  Each hot-key is
            //       responsible for cleaning up after itself; therefore,
            //       this class does not need to worry about cleaning up
            //       after them.
            //
            lock (syncRoot) /* REDUNDANT */
            {
                notify = true; /* NOTE: Notifications are ON by default. */
                logging = false; /* NOTE: Logging is OFF by default. */
                hotKeys = new Dictionary<int, IHotKey>();
                hooks = new Dictionary<HotKeyHookType, string>();
                nextId = 0;
            }

            //
            // NOTE: Register the event handlers.
            //
            this.Shown += new EventHandler(HotKeyManagerForm_Shown);
            this.KeyDown += new KeyEventHandler(HotKeyManagerForm_KeyDown);
            this.KeyUp += new KeyEventHandler(HotKeyManagerForm_KeyUp);
            this.Resize += new EventHandler(HotKeyManagerForm_Resize);

            this.FormClosing += new FormClosingEventHandler(
                HotKeyManagerForm_FormClosing);

            this.FormClosed += new FormClosedEventHandler(
                HotKeyManagerForm_FormClosed);

            this.Disposed += new EventHandler(HotKeyManagerForm_Disposed);

            notHotKey.Click += new EventHandler(notHotKey_Click);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHotKeyManager Members
        /// <summary>
        /// Gets a value indicating whether the manager has been closed.
        /// </summary>
        public bool IsClosed /* NO-LOCK? */
        {
            get { /* CheckDisposed(); */ return disposed; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets a value indicating whether the manager emits
        /// notifications for hot-key events.
        /// </summary>
        public bool Notify /* NO-LOCK? */
        {
            get { CheckDisposed(); return notify; }
            set { CheckDisposed(); notify = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets a value indicating whether the manager is logging.
        /// </summary>
        public bool Logging /* NO-LOCK? */
        {
            get { CheckDisposed(); return logging; }
            set { CheckDisposed(); logging = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets the title of the manager form.
        /// </summary>
        public string Title /* NO-LOCK? */
        {
            get
            {
                CheckDisposed();

                string text = null;

                return WinFormsOps.GetText(this, ref text) ? text : null;
            }
            set
            {
                CheckDisposed();

                WinFormsOps.SetText(this, value, false);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the manager's form.
        /// </summary>
        /// <returns>
        /// The hot-key manager form.
        /// </returns>
        public Form GetHotKeyManagerForm()
        {
            CheckDisposed();

            return this;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the window handle that hot-keys are registered against.
        /// </summary>
        /// <returns>
        /// The hot-key window handle.
        /// </returns>
        public IntPtr GetHotKeyHandle()
        {
            CheckDisposed();

            lock (syncRoot)
            {
                return handle;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the next available hot-key id.
        /// </summary>
        /// <returns>
        /// A new, unused hot-key id.
        /// </returns>
        public int GetNextHotKeyId()
        {
            CheckDisposed();

            return Interlocked.Increment(ref nextId);
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode GetHookScriptFor(
            HotKeyHookType type, /* in */
            ref string text,     /* out */
            ref Result error     /* out */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (hooks == null)
                {
                    error = "hooks unavailable";
                    return ReturnCode.Error;
                }

                type &= HotKeyHookType.BaseTypeMask;

                /* IGNORED */
                hooks.TryGetValue(type, out text);

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode SetHookScriptFor(
            HotKeyHookType type, /* in */
            ref string text,     /* in, out */
            ref Result error     /* out */
            )
        {
            CheckDisposed();

            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (hooks == null)
                {
                    error = "hooks unavailable";
                    return ReturnCode.Error;
                }

                type &= HotKeyHookType.BaseTypeMask;
                hooks[type] = text;

                return ReturnCode.Ok;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets data describing the previous hot-key event processed by the
        /// manager.
        /// </summary>
        /// <param name="result">
        /// On output, receives the previous event data, or an error message.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode GetPreviousEventData(
            ref Result result /* out */
            )
        {
            CheckDisposed();

            StringList[] lists = { null, null, null };

            lists[0] = WinFormsOps.ToList("KeyDown",
                Interlocked.CompareExchange(ref previousKeyDownEventArgs,
                null, null));

            lists[1] = WinFormsOps.ToList("KeyUp",
                Interlocked.CompareExchange(ref previousKeyUpEventArgs,
                null, null));

#if NATIVE && WINDOWS
            lock (syncRoot)
            {
                lists[2] = WinFormsOps.ToList("KeyHit", previousHotKey);
            }
#endif

            StringList list = new StringList();

            for (int index = 0; index < lists.Length; index++)
            {
                if (lists[index] == null)
                    continue;

                list.Add(lists[index].ToString());
            }

            result = list;
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Counts the defined hot-keys.
        /// </summary>
        /// <param name="registered">
        /// Non-zero to count only registered hot-keys.
        /// </param>
        /// <param name="count">
        /// On output, receives the count.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode CountHotKeys(
            bool registered, /* in */
            ref int count,   /* out */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (registered)
                    {
                        int localCount = 0;

                        foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                        {
                            IHotKey hotKey = pair.Value;

                            if (hotKey == null)
                                continue;

                            if (hotKey.Registered)
                                localCount++;
                        }

                        count = localCount;
                    }
                    else
                    {
                        count = hotKeys.Count;
                    }
                }
            }
            finally
            {
                /* NO RESULT */
                LogOperation("CountHotKeys");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the defined hot-keys.
        /// </summary>
        /// <param name="unregisterOnly">
        /// Non-zero to only unregister, keeping definitions.
        /// </param>
        /// <param name="force">
        /// Non-zero to clear even when normally disallowed.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode ClearHotKeys(
            bool unregisterOnly, /* in */
            bool force,          /* in */
            ref Result error     /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (force)
                    {
                        foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                        {
                            IHotKey hotKey = pair.Value;

                            if ((hotKey == null) || !hotKey.Registered)
                                continue;

                            ReturnCode code;

                            try
                            {
                                code = hotKey.Unregister(ref error); /* throw */
                            }
                            catch (Exception e)
                            {
                                error = e;
                                code = ReturnCode.Error;
                            }

                            if (code != ReturnCode.Ok)
                                return code;
                        }
                    }
                    else
                    {
                        ReturnCode code;
                        int count = 0;

                        code = CountHotKeys(true, ref count, ref error);

                        if (code != ReturnCode.Ok)
                            return code;

                        if (count > 0)
                        {
                            error = String.Format(
                                "cannot clear hot-keys, {0} are registered",
                                count);

                            return ReturnCode.Error;
                        }

                        foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                        {
                            IHotKey hotKey = pair.Value;

                            if (hotKey == null)
                                continue;

                            if (hotKey.Registered) /* REDUNDANT (?) */
                            {
                                error = String.Format(
                                    "cannot clear registered hot-key {0}",
                                    hotKey.Id);

                                return ReturnCode.Error;
                            }
                        }
                    }

                    if (!unregisterOnly)
                        hotKeys.Clear();
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();

                /* NO RESULT */
                LogOperation("ClearHotKeys");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode ListHotKeys(
            ref IntList ids, /* in, out */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (ids == null)
                        ids = new IntList(hotKeys.Keys);
                }
            }
            finally
            {
                /* NO RESULT */
                LogOperation("ListHotKeys");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// Non-zero to return all matches; zero for the first.
        /// </param>
        /// <param name="ids">
        /// On output, receives the matching ids.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode FindHotKeys(
            Keys keys,       /* in */
            bool exact,      /* in */
            bool all,        /* in */
            ref IntList ids, /* in, out */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (ids == null)
                        ids = new IntList();

                    foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                    {
                        IHotKey hotKey = pair.Value;

                        if (hotKey == null)
                            continue;

                        if ((exact && (hotKey.Keys == keys)) || (!exact &&
                            WinFormsOps.HasKeys(hotKey.Keys, keys, true)))
                        {
                            ids.Add(hotKey.Id);

                            if (!all)
                                return ReturnCode.Ok;
                        }
                    }
                }
            }
            finally
            {
                /* NO RESULT */
                LogOperation("FindHotKeys(Keys)");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// Non-zero to return all matches; zero for the first.
        /// </param>
        /// <param name="ids">
        /// On output, receives the matching ids.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode FindHotKeys(
            HotKeyFlags flags, /* in */
            bool exact,        /* in */
            bool all,          /* in */
            ref IntList ids,   /* in, out */
            ref Result error   /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (ids == null)
                        ids = new IntList();

                    foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                    {
                        IHotKey hotKey = pair.Value;

                        if (hotKey == null)
                            continue;

                        if ((exact && (hotKey.Flags == flags)) ||
                            (!exact && hotKey.HasFlags(flags, true)))
                        {
                            ids.Add(hotKey.Id);

                            if (!all)
                                return ReturnCode.Ok;
                        }
                    }
                }
            }
            finally
            {
                /* NO RESULT */
                LogOperation("FindHotKeys(Flags)");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds the ids of hot-keys by registration state.
        /// </summary>
        /// <param name="registered">
        /// The registration state to match.
        /// </param>
        /// <param name="all">
        /// Non-zero to return all matches; zero for the first.
        /// </param>
        /// <param name="ids">
        /// On output, receives the matching ids.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode FindHotKeys(
            bool registered, /* in */
            bool all,        /* in */
            ref IntList ids, /* in, out */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (ids == null)
                        ids = new IntList();

                    foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                    {
                        IHotKey hotKey = pair.Value;

                        if (hotKey == null)
                            continue;

                        if (hotKey.Registered == registered)
                        {
                            ids.Add(hotKey.Id);

                            if (!all)
                                return ReturnCode.Ok;
                        }
                    }
                }
            }
            finally
            {
                /* NO RESULT */
                LogOperation("FindHotKeys(Registered)");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode GetHotKey(
            IGetValue getValue,      /* in */
            CultureInfo cultureInfo, /* in */
            ref IHotKey hotKey,      /* out */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            int id = 0;

            if (Value.GetInteger2(
                    getValue, ValueFlags.AnyInteger, cultureInfo,
                    ref id, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return GetHotKey(id, ref hotKey, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode GetHotKey(
            int id,             /* in */
            ref IHotKey hotKey, /* out */
            ref Result error    /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    IHotKey localHotKey;

                    if (!hotKeys.TryGetValue(id, out localHotKey))
                    {
                        error = String.Format(
                            "hot-key {0} not found", id);

                        return ReturnCode.Error;
                    }

                    if (localHotKey == null)
                    {
                        error = String.Format(
                            "hot-key {0} is invalid", id);

                        return ReturnCode.Error;
                    }

                    hotKey = localHotKey;
                }
            }
            finally
            {
                /* NO RESULT */
                LogOperation("GetHotKey");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets (replaces) the hot-key identified by a value resolvable to a
        /// hot-key id.
        /// </summary>
        /// <param name="getValue">
        /// The value identifying the hot-key.
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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode SetHotKey(
            IGetValue getValue,      /* in */
            CultureInfo cultureInfo, /* in */
            IHotKey hotKey,          /* in */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            int id = 0;

            if (Value.GetInteger2(
                    getValue, ValueFlags.AnyInteger, cultureInfo,
                    ref id, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return SetHotKey(id, hotKey, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode SetHotKey(
            int id,          /* in */
            IHotKey hotKey,  /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (hotKey == null)
                    {
                        error = String.Format(
                            "new hot-key {0} is invalid", id);

                        return ReturnCode.Error;
                    }

                    if (hotKey.Id != id)
                    {
                        error = String.Format(
                            "new hot-key {0} does not match old hot-key id {1}",
                            hotKey.Id, id);

                        return ReturnCode.Error;
                    }

                    IHotKey localHotKey;

                    if (!hotKeys.TryGetValue(id, out localHotKey))
                    {
                        error = String.Format(
                            "hot-key {0} not found", id);

                        return ReturnCode.Error;
                    }

                    //
                    // BUGFIX: There is no need to replace a hot-key with
                    //         itself; furthermore, if we dispose of it,
                    //         there will be object disposal errors later
                    //         when the hot-key manager iterates over its
                    //         collection of hot-keys.
                    //
                    if (Object.ReferenceEquals(localHotKey, hotKey))
                        return ReturnCode.Ok;

                    if (localHotKey != null)
                    {
                        if (localHotKey.Registered)
                        {
                            error = String.Format(
                                "cannot replace registered hot-key {0}", id);

                            return ReturnCode.Error;
                        }

                        IDisposable disposable = localHotKey as IDisposable;

                        if (disposable != null)
                            disposable.Dispose(); /* throw */
                    }

                    hotKeys[id] = hotKey;
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();

                /* NO RESULT */
                LogOperation("SetHotKey");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode AddHotKey(
            IHotKey hotKey,  /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (hotKey == null)
                    {
                        error = "hot-key is invalid";
                        return ReturnCode.Error;
                    }

                    if (hotKeys.ContainsKey(hotKey.Id))
                    {
                        error = String.Format(
                            "cannot add hot-key {0}, already exists",
                            hotKey.Id);

                        return ReturnCode.Error;
                    }

                    hotKeys.Add(hotKey.Id, hotKey);
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();

                /* NO RESULT */
                LogOperation("AddHotKey(IHotKey)");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates and adds a new hot-key for the specified keys, flags, and
        /// text.
        /// </summary>
        /// <param name="keys">
        /// The key combination.
        /// </param>
        /// <param name="flags">
        /// The hot-key flags.
        /// </param>
        /// <param name="text">
        /// The descriptive text.
        /// </param>
        /// <param name="id">
        /// On output, receives the assigned id.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode AddHotKey(
            Keys keys,         /* in */
            HotKeyFlags flags, /* in */
            string text,       /* in */
            ref int id,        /* out */
            ref Result error   /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    int localId = Interlocked.Increment(ref nextId);

                    hotKeys.Add(localId,
                        HotKey.Components.Private.HotKey.Create(this,
                            handle, localId, keys, flags, text));

                    id = localId;
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();

                /* NO RESULT */
                LogOperation("AddHotKey(...)");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes the hot-key identified by a value resolvable to a hot-key
        /// id.
        /// </summary>
        /// <param name="getValue">
        /// The value identifying the hot-key.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture used when resolving the value.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode RemoveHotKey(
            IGetValue getValue,      /* in */
            CultureInfo cultureInfo, /* in */
            ref Result error         /* out */
            )
        {
            CheckDisposed();

            int id = 0;

            if (Value.GetInteger2(
                    getValue, ValueFlags.AnyInteger, cultureInfo,
                    ref id, ref error) != ReturnCode.Ok)
            {
                return ReturnCode.Error;
            }

            return RemoveHotKey(id, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

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
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode RemoveHotKey(
            int id,          /* in */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    IHotKey hotKey;

                    if (!hotKeys.TryGetValue(id, out hotKey))
                    {
                        error = String.Format(
                            "hot-key {0} not found", id);

                        return ReturnCode.Error;
                    }

                    if ((hotKey != null) && hotKey.Registered)
                    {
                        error = String.Format(
                            "cannot remove registered hot-key {0}", id);

                        return ReturnCode.Error;
                    }

                    if (!hotKeys.Remove(id))
                    {
                        error = String.Format(
                            "hot-key {0} not found", id);

                        return ReturnCode.Error;
                    }
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();

                /* NO RESULT */
                LogOperation("RemoveHotKey");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads hot-key definitions from the supplied text.
        /// </summary>
        /// <param name="text">
        /// The text containing the definitions.
        /// </param>
        /// <param name="strictCount">
        /// Non-zero to require the expected count.
        /// </param>
        /// <param name="strictRegister">
        /// Non-zero to require each to register.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode LoadHotKeys(
            string text,         /* in */
            bool strictCount,    /* in */
            bool strictRegister, /* in */
            ref Result error     /* out */
            )
        {
            CheckDisposed();

            try
            {
                int oldCount;

                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    if (interpreter == null)
                    {
                        error = "invalid interpreter";
                        return ReturnCode.Error;
                    }

                    /* NO RESULT */
                    SaveHotKeyRegisteredStates();

                    oldCount = hotKeys.Count;
                }

                ReturnCode code;
                Result result = null;

                code = ScriptOps.EvaluateForLoad(
                    interpreter, text, SafeAppendLogEntry, ref result);

                if (code != ReturnCode.Ok)
                {
                    error = result;
                    return code;
                }

                lock (syncRoot)
                {
                    int newCount = hotKeys.Count;

                    //
                    // NOTE: In "strict" mode, fail if the number of hot-keys
                    //       is unchanged after evaluating the load script.
                    //
                    if (strictCount && (newCount == oldCount))
                    {
                        error = String.Format(
                            "count of hot-keys unchanged at {0}", newCount);

                        return ReturnCode.Error;
                    }

                    code = RestoreHotKeyRegisteredStates(ref error);

                    if (code != ReturnCode.Ok)
                    {
                        if (strictRegister)
                            return code;

                        code = ReturnCode.Ok;
                    }
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();

                /* NO RESULT */
                LogOperation("LoadHotKeys");
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Serializes the current hot-key definitions to text.
        /// </summary>
        /// <param name="strict">
        /// Non-zero to enforce stricter serialization.
        /// </param>
        /// <param name="text">
        /// On output, receives the serialized text.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode SaveHotKeys(
            bool strict,     /* in */
            ref string text, /* in, out */
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    //
                    // NOTE: Start building a script (text string) that can be
                    //       evaluated in order to reconstruct the currently
                    //       configured set of hot-keys, including their states
                    //       of registration.
                    //
                    IScriptBuilder scriptBuilder = ScriptBuilder.Create();
                    string newLineString = NewLine.ToString();

                    foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                    {
                        IHotKey hotKey = pair.Value;

                        if (hotKey == null)
                            continue;

                        //
                        // NOTE: Make sure there is at least some vertical
                        //       space between saved hot-keys.
                        //
                        if (!strict && (scriptBuilder.Count > 0) &&
                            (scriptBuilder.Add(
                                newLineString, ref error) != ReturnCode.Ok))
                        {
                            return ReturnCode.Error;
                        }

                        //
                        // NOTE: If this hot-key is registered, set the flag
                        //       accordingly; otherwise, clear the flag.
                        //
                        if (hotKey.Registered)
                            hotKey.SetWasRegistered();
                        else
                            hotKey.ClearWasRegistered();

                        //
                        // NOTE: Build a command to add this hot-key and then
                        //       attempt to add the built command to the save
                        //       script.
                        //
                        IStringList arguments = new StringList();

                        arguments.Add(new string[] {
                            ScriptOps.commandName, ScriptOps.addSubCommandName,
                            hotKey.Keys.ToString(), hotKey.Flags.ToString(),
                            ScriptOps.GetTextToSave(hotKey.Text, true)
                        });

                        if (scriptBuilder.Add(
                                arguments, ref error) != ReturnCode.Ok)
                        {
                            return ReturnCode.Error;
                        }
                    }

                    text = String.Format(
                        "{0}{1}{2}{1}", strict ? String.Empty :
                        String.Format(FileHeader, LogOps.GetNowString(
                        false), Environment.MachineName, Environment.UserName),
                        strict ? String.Empty : NewLine.ToString(),
                        scriptBuilder.ToString());
                }
            }
            finally
            {
                /* NO RESULT */
                LogOperation("SaveHotKeys");
            }

            return ReturnCode.Ok;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ISafeResult Members
        /// <summary>
        /// Clears the result/log display safely from any thread.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool SafeClearResult()
        {
            CheckDisposed();

            return WinFormsOps.SetText(txtResult, null, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text as a log entry safely from any thread.
        /// </summary>
        /// <param name="text">
        /// The log entry text to append.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool SafeAppendLogEntry(
            string text /* in */
            )
        {
            CheckDisposed();

            return SafeAppendResult(LogOps.FormatHotKeyLogEntry(text));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies the result/log display to the clipboard safely from any
        /// thread.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool SafeCopyResultToClipboard()
        {
            CheckDisposed();

            return WinFormsOps.CopyTextToClipboard(txtResult, true);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Caches the manager window handle for use from other threads.
        /// </summary>
        private void CacheHandle()
        {
            //
            // HACK: Cache the IntPtr handle because it will be needed by
            //       non-primary [script] threads.  This method will only work
            //       properly from the primary thread for the form.  On other
            //       threads, it will throw an exception.
            //
            lock (syncRoot)
            {
                //
                // NOTE: Note the text casing here.  The lowercase "handle" is
                //       used to refer to the IntPtr field of this class and
                //       the uppercase "Handle" is used to refer to the IntPtr
                //       property of the System.Windows.Forms.Form class.
                //
                this.handle = this.Handle;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logs a message describing a manager operation.
        /// </summary>
        /// <param name="operation">
        /// The operation to log.
        /// </param>
        private void LogOperation(
            string operation /* in */
            )
        {
            if (!logging || String.IsNullOrEmpty(operation))
                return;

            SafeAppendLogEntry(String.Format("LogOperation: {0}", operation));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the notify (tray) icon needs to be set.
        /// </summary>
        /// <returns>
        /// Non-zero when the notify icon must be set; otherwise, zero.
        /// </returns>
        private bool NeedToSetNotifyIcon()
        {
            //
            // NOTE: If we are minimized, the ShowInTaskbar property should be
            //       false; otherwise, it should be true.  This method returns
            //       a non-zero value if the previous truth statement does NOT
            //       currently hold true.
            //
            if (this.WindowState == FormWindowState.Minimized)
                return this.ShowInTaskbar;
            else
                return !this.ShowInTaskbar;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets or clears the notify (tray) icon according to the minimized
        /// state.
        /// </summary>
        /// <param name="minimized">
        /// Non-zero when the form is minimized.
        /// </param>
        private void SetNotifyIcon(
            bool minimized /* in */
            )
        {
            if (minimized)
            {
                notHotKey.Visible = true;
                this.ShowInTaskbar = false;
            }
            else
            {
                this.ShowInTaskbar = true;
                notHotKey.Visible = false;
            }

            SafeAppendLogEntry(String.Format("SetNotifyIcon: set to {0}",
                minimized ? "visible" : "not visible"));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the registered state of each hot-key so it can be restored
        /// later.
        /// </summary>
        private void SaveHotKeyRegisteredStates()
        {
            SafeAppendLogEntry("SaveHotKeyRegisteredStates: entered");

            lock (syncRoot)
            {
                bool saveNotify = notify;
                notify = false;

                try
                {
                    if (hotKeys == null)
                        return;

                    foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                    {
                        IHotKey hotKey = pair.Value;

                        if (hotKey == null)
                            continue;

                        //
                        // NOTE: If this hot-key is registered, set the flag
                        //       accordingly; otherwise, clear the flag.  This
                        //       is necessary so that we do not unregister any
                        //       currently registered hot-keys after evaluating
                        //       the hot-key script, below.
                        //
                        if (hotKey.Registered)
                            hotKey.SetWasRegistered();
                        else
                            hotKey.ClearWasRegistered();
                    }
                }
                finally
                {
                    notify = saveNotify;
                    saveNotify = false;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the previously saved registered state of each hot-key.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private ReturnCode RestoreHotKeyRegisteredStates(
            ref Result error /* out */
            )
        {
            SafeAppendLogEntry("RestoreHotKeyRegisteredStates: entered");

            lock (syncRoot)
            {
                bool saveNotify = notify;
                notify = false;

                try
                {
                    if (hotKeys == null)
                    {
                        error = "hot-keys are not available";
                        return ReturnCode.Error;
                    }

                    foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                    {
                        IHotKey hotKey = pair.Value;

                        if (hotKey == null)
                            continue;

                        //
                        // NOTE: If this hot-key was registered upon last being
                        //       saved and is not registered now, then register
                        //       it.  Otherwise, if this hot-key was not
                        //       registered upon last being saved and is
                        //       registered now, then unregister it.
                        //
                        if (hotKey.HasFlags(HotKeyFlags.WasRegistered, true))
                        {
                            if (hotKey.Registered)
                            {
                                //
                                // NOTE: The hot-key is still registered, clear
                                //       the flag.
                                //
                                hotKey.ClearWasRegistered();
                            }
                            else
                            {
                                //
                                // NOTE: Re-register the hot-key and then clear
                                //       the flag upon success.
                                //
                                ReturnCode code = hotKey.Register(ref error);

                                if (code == ReturnCode.Ok)
                                    hotKey.ClearWasRegistered();
                                else
                                    return code;
                            }
                        }
                        else if (hotKey.Registered)
                        {
                            //
                            // NOTE: The hot-key is still registered and it
                            //       should not be; therefore, unregister it.
                            //
                            ReturnCode code = hotKey.Unregister(ref error);

                            if (code != ReturnCode.Ok)
                                return code;
                        }
                    }
                }
                finally
                {
                    notify = saveNotify;
                    saveNotify = false;
                }
            }

            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Notifies all hot-keys that the manager window (their parent) has
        /// changed.
        /// </summary>
        private void NotifyHotKeysOfParentChange()
        {
            lock (syncRoot)
            {
                if (hotKeys == null)
                    return;

                foreach (KeyValuePair<int, IHotKey> pair in hotKeys)
                {
                    IHotKey hotKey = pair.Value;

                    if (hotKey == null)
                        continue;

                    /* NO RESULT */
                    hotKey.ParentHasChanged(this, handle);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text to the result display.
        /// </summary>
        /// <param name="text">
        /// The text to append.
        /// </param>
        private bool SafeAppendResult(
            string text /* in */
            )
        {
            return WinFormsOps.AppendText(txtResult, text, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Counts the defined hot-keys, ignoring any error.
        /// </summary>
        /// <param name="registered">
        /// Non-zero to count only registered hot-keys.
        /// </param>
        /// <param name="count">
        /// On output, receives the count.
        /// </param>
        private ReturnCode CountHotKeys(
            bool registered, /* in */
            ref int count    /* out */
            )
        {
            Result error = null;

            return CountHotKeys(registered, ref count, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates the on-close hook script to decide whether the manager
        /// may close.
        /// </summary>
        /// <param name="eventArgs">
        /// The closing event arguments.
        /// </param>
        /// <returns>
        /// Non-zero when closing is allowed; otherwise, zero.
        /// </returns>
        private bool EvaluateForClosing(
            FormClosingEventArgs eventArgs /* in */
            )
        {
            TracePriority priority =
                TracePriority.Highest | TracePriority.FromPlugin;

            if (eventArgs == null)
            {
                Utility.DebugTrace(
                     "EvaluateForClosing: invalid EventArgs",
                    typeof(HotKeyManagerForm).Name, priority);

                return true; /* CONTINUE IN CALLER */
            }

            if ((interpreter != null) && interpreter.Disposing)
            {
                Utility.DebugTrace(
                     "EvaluateForClosing: interpreter disposing",
                    typeof(HotKeyManagerForm).Name, priority);

                return true; /* CONTINUE IN CALLER */
            }

            ReturnCode code;
            string text = null;
            Result error = null;

            code = GetHookScriptFor(
                HotKeyHookType.OnManagerClose, ref text, ref error);

            if (code != ReturnCode.Ok)
                LogOps.Complain(interpreter, code, error);

            if (text != null)
            {
                ReturnCode scriptCode;
                Result scriptResult = null;

                scriptCode = ScriptOps.Evaluate(
                    interpreter, text, false, false, true, false,
                    ref scriptResult);

                switch (scriptCode)
                {
                    case ReturnCode.Ok:
                        {
                            // do nothing, normal operation.
                            Utility.AdjustTracePriority(
                                ref priority, -5);

                            Utility.DebugTrace(String.Format(
                                 "EvaluateForClosing: OK {0}",
                                 Utility.FormatResult(
                                    scriptCode, scriptResult)),
                                typeof(HotKeyManagerForm).Name,
                                priority);

                            break;
                        }
                    case ReturnCode.Error:
                        {
                            // do nothing, report error.
                            Utility.DebugTrace(String.Format(
                                 "EvaluateForClosing: ERROR {0}",
                                 Utility.FormatResult(
                                    scriptCode, scriptResult)),
                                typeof(HotKeyManagerForm).Name,
                                priority);

                            break;
                        }
                    case ReturnCode.Return:
                        {
                            // cancel closing the manager form.
                            LogOps.LogOrComplain(
                                interpreter, "SCRIPT CANCELED CLOSING");

                            eventArgs.Cancel = true;
                            return false; /* RETURN FROM CALLER */
                        }
                    default:
                        {
                            Utility.DebugTrace(String.Format(
                                "EvaluateForClosing: UNSUPPORTED CODE {0}",
                                Utility.FormatResult(
                                    scriptCode, scriptResult)),
                                typeof(HotKeyManagerForm).Name,
                                priority);

                            break;
                        }
                }
            }

            return true; /* CONTINUE IN CALLER */
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Prompts the user to confirm closing the manager.
        /// </summary>
        /// <returns>
        /// Non-zero when the user confirmed closing; otherwise, zero.
        /// </returns>
        private bool PromptToClose()
        {
            //
            // TODO: There is almost no point in prompting the user if there
            //       are no registered hot-keys.  In the future, maybe there
            //       needs to be a setting for the hot-key manager that to
            //       "always prompt user to close hot-key manager form"?
            //
            int count = 0;

            if ((CountHotKeys(true, ref count) == ReturnCode.Ok) &&
                (count == 0))
            {
                //
                // NOTE: There are currently no registered hot-keys.  Avoid
                //       prompting user and just allow the form to close.
                //
                return true;
            }

            return (WinFormsOps.YesOrNo(this, String.Format(
                ClosingQuestionText, count)) == DialogResult.Yes);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Event Handlers
        /// <summary>
        /// Handles the form-shown event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyManagerForm_Shown(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            try
            {
                if (@event != null)
                    @event.Set(); /* throw */

                //
                // NOTE: Show that the manager has been loaded into a
                //       particular thread.
                //
                SafeAppendLogEntry(String.Format(
                    "HotKeyManagerForm_Shown: On thread {0}",
                    Utility.GetCurrentThreadId()));

                //
                // NOTE: Start the hot-key manager form minimized?
                //
                if (interpreter == null)
                    return;

                bool locked = false;

                try
                {
                    interpreter.TryLock(MinimizedVariableTimeout,
                        ref locked); /* TRANSACTIONAL */

                    if (locked)
                    {
                        if (interpreter.DoesVariableExist(
                                VariableFlags.None,
                                MinimizedVariableName) == ReturnCode.Ok)
                        {
                            this.WindowState = FormWindowState.Minimized;
                        }
                    }
                }
                finally
                {
                    interpreter.ExitLock(ref locked); /* TRANSACTIONAL */
                }
            }
            catch (Exception ex)
            {
                LogOps.Complain(interpreter, ReturnCode.Error, ex);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the key-down event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyManagerForm_KeyDown(
            object sender, /* in */
            KeyEventArgs e /* in */
            )
        {
            Interlocked.Exchange(ref previousKeyDownEventArgs, e);

            if (e == null)
                return;

            SafeAppendLogEntry(String.Format(
                "DOWN: KeyValue = {0}, KeyData = {1}, Modifiers = {2}, " +
                "KeyCode = {3}, Shift = {4}, Control = {5}, Alt = {6}, " +
                "SuppressKeyPress = {7}, Handled = {8}", e.KeyValue,
                e.KeyData, e.Modifiers, e.KeyCode, e.Shift, e.Control,
                e.Alt, e.SuppressKeyPress, e.Handled));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the key-up event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyManagerForm_KeyUp(
            object sender, /* in */
            KeyEventArgs e /* in */
            )
        {
            Interlocked.Exchange(ref previousKeyUpEventArgs, e);

            if (e == null)
                return;

            SafeAppendLogEntry(String.Format(
                "UP: KeyValue = {0}, KeyData = {1}, Modifiers = {2}, " +
                "KeyCode = {3}, Shift = {4}, Control = {5}, Alt = {6}, " +
                "SuppressKeyPress = {7}, Handled = {8}", e.KeyValue,
                e.KeyData, e.Modifiers, e.KeyCode, e.Shift, e.Control,
                e.Alt, e.SuppressKeyPress, e.Handled));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the form-resize event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyManagerForm_Resize(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            //
            // NOTE: This event handler MUST only deal with "size" changes that
            //       require modification of the notification icon state.
            //
            if (!NeedToSetNotifyIcon())
                return;

            //
            // NOTE: Save the current "registered" states of all the currently
            //       configured hot-keys now.  There will be an attempt to
            //       restore these states after the native Win32 window handle
            //       has been re-created.
            //
            /* NO RESULT */
            SaveHotKeyRegisteredStates();

            //
            // NOTE: Make sure any existing hot-keys are unregistered prior to
            //       changing the ShowInTaskbar property (below) because the
            //       native Win32 window handle will be invalidated by that
            //       change, thus making the any currently registered hot-keys
            //       non-functional anyhow.
            //
            ReturnCode code;
            Result error = null;

            code = ClearHotKeys(true, true, ref error);

            if (code != ReturnCode.Ok)
            {
                LogOps.Complain(interpreter, code, error);
                return;
            }

            //
            // NOTE: Make sure the system tray icon is visible if and only if
            //       the current window state is minimized.
            //
            /* NO RESULT */
            SetNotifyIcon(this.WindowState == FormWindowState.Minimized);

            //
            // BUGFIX: Changing the ShowInTaskbar property apparently always
            //         causes the native Win32 window handle to be recreated;
            //         therefore, re-cache it now.
            //
            /* NO RESULT */
            CacheHandle();

            //
            // BUGFIX: Apparently, the .NET Framework WinForms code will lose
            //         track of this form (i.e. it will be missing from the
            //         Application.OpenForms collection); therefore, manually
            //         make sure it is re-added, if necessary.
            //
            /* NO RESULT */
            SynchronizeWithOpenForms();

            //
            // BUGFIX: Also, notify all the currently configured hot-keys that
            //         their parent native Win32 window handle has just been
            //         changed.
            //
            /* NO RESULT */
            NotifyHotKeysOfParentChange();

            //
            // NOTE: Attempt to restore the previously registered hot-keys now
            //       that the native Win32 window handle has been updated
            //       within each of them.
            //
            code = RestoreHotKeyRegisteredStates(ref error);

            if (code != ReturnCode.Ok)
                LogOps.Complain(interpreter, code, error);

            //
            // NOTE: Notify any active hot-key viewer forms that the list of
            //       hot-keys has been [possibly] modified.  This must be done
            //       last in this event handler because several of the previous
            //       code blocks modify the currently configured hot-keys.
            //       This [final] call to the NotifyHotKeyViewForms method may
            //       actually be superfluous (i.e. if one was already made as
            //       the last act of the previous code block).
            //
            /* NO RESULT */
            ScriptOps.NotifyViewForms();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the form-closing event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyManagerForm_FormClosing(
            object sender,         /* in */
            FormClosingEventArgs e /* in */
            )
        {
            if (!EvaluateForClosing(e)) /* SCRIPT HOOK */
                return;

            if (InSafeClose() ||
                (e == null) || (e.CloseReason != CloseReason.UserClosing) ||
                PromptToClose())
            {
                //
                // NOTE: Before closing, make sure that the resources for
                //       the notification icon get disposed of properly.
                //
                SetNotifyIcon(false);
            }
            else
            {
                e.Cancel = true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the form-closed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyManagerForm_FormClosed(
            object sender,        /* in */
            FormClosedEventArgs e /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = ClearHotKeys(false, true, ref error);

            if (code != ReturnCode.Ok)
                LogOps.Complain(interpreter, code, error);

            Shell.Form.ClearHotKeyManager();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the disposed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyManagerForm_Disposed(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            if (!disposed)
            {
                //
                // NOTE: This form is now disposed.
                //
                disposed = true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the notify-icon click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void notHotKey_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            //
            // NOTE: If the current window state is minimized, reset it to
            //       normal.  This will cause the Resize event to be fired,
            //       thus hiding this system tray icon.
            //
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Windows.Forms.Form Overrides
        /// <summary>
        /// Processes Windows messages, intercepting the hot-key activation
        /// message to dispatch the corresponding hot-key script.
        /// </summary>
        /// <param name="m">
        /// The Windows message being processed.
        /// </param>
        protected override void WndProc(ref Message m)
        {
#if NATIVE && WINDOWS
            if (m.Msg == HotKey.Components.Private.HotKey.MessageId)
            {
                IHotKey hotKey;

                lock (syncRoot)
                {
                    previousHotKey = null;

                    if ((hotKeys == null) ||
                        !hotKeys.TryGetValue(m.WParam.ToInt32(), out hotKey))
                    {
                        goto done;
                    }

                    previousHotKey = hotKey;
                }

                //
                // NOTE: Unless the hot-key is flagged to prevent logging of
                //       the native Win32 "hot-key hit" event, append to the
                //       log now.
                //
                if (!hotKey.HasFlags(HotKeyFlags.NoLogHit, true))
                {
                    SafeAppendLogEntry(String.Format(
                        "HIT: keyId = {0}, modifiers = {1}, virtualKey = {2}",
                        hotKey.Id, Utility.FormatWrapOrNull(hotKey.Modifiers),
                        Utility.FormatWrapOrNull(hotKey.VirtualKey)));
                }

                try
                {
                    if (!hotKey.HasFlags(HotKeyFlags.NoResetResult, true))
                        /* NO RESULT */
                        hotKey.ResetResult(); /* throw */

                    /* NO RESULT */
                    hotKey.EvaluateScript(interpreter,
                        HotKeyScriptFlags.ViaHotKeyEvent); /* throw */
                }
                catch (Exception e)
                {
                    if (!hotKey.HasFlags(HotKeyFlags.NoLogError, true))
                    {
                        SafeAppendLogEntry(String.Format(
                            "ERROR: keyId = {0}, exception = {1}", hotKey.Id,
                            Utility.FormatTraceException(e)));
                    }
                }

                //
                // NOTE: If the key is flagged as "fully handled", that means
                //       we need to skip the normal Windows default processing.
                //       Yes, this can be somewhat dangerous.
                //
                if (hotKey.HasFlags(HotKeyFlags.FullyHandled, true))
                    return;
            }

        done:
#endif

            //
            // NOTE: *IMPORTANT* Normally, we always want the normal default
            //       Windows processing as well.
            //
            base.WndProc(ref m);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable "Pattern" Members
        /// <summary>
        /// Non-zero if this instance has been disposed.
        /// </summary>
        private bool disposed;
        /// <summary>
        /// Throws an exception if this instance has already been disposed.
        /// </summary>
        private void CheckDisposed() /* throw */
        {
#if THROW_ON_DISPOSED
            if (disposed && Engine.IsThrowOnDisposed(interpreter, null))
            {
                throw new ObjectDisposedException(
                    typeof(HotKeyManagerForm).Name);
            }
#endif
        }
        #endregion
    }
    #endregion
}
