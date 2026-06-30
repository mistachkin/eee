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

#if NATIVE && WINDOWS
using System.Runtime.InteropServices;
using System.Security;

#if !NET_40
using System.Security.Permissions;
#endif
#endif

using System.Threading;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;
using HotKey.Interfaces.Private;

namespace HotKey.Components.Private
{
    /// <summary>
    /// Implements a single global hot-key (the <see cref="IHotKey" />
    /// interface): its key combination, descriptive text, registration and hit
    /// state, and the script and captured result/error information for its
    /// activation.  It registers and unregisters the hot-key with the
    /// operating system and evaluates its script on activation.
    /// </summary>
#if NATIVE && WINDOWS
#if NET_40
    [SecurityCritical()]
#else
    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
#endif
#endif
    [ObjectId("cd251ff2-e3d6-4a2f-aaf6-b2460a2fc01a")]
    internal sealed class HotKey :
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
            ScriptMarshalByRefObject,
#endif
            IHotKey,
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
            IAsynchronousCallback,
#endif
            ICloneable, IDisposable
    {
        #region Unsafe Native Methods Class
#if NATIVE && WINDOWS
        /// <summary>
        /// Contains the native Win32 hot-key constants and functions (User32)
        /// used to register and unregister global hot-keys.
        /// </summary>
        [SuppressUnmanagedCodeSecurity()]
        [ObjectId("ba5b2874-b6ef-4b53-b4e5-fb8afd65940f")]
        internal static class UnsafeNativeMethods
        {
            #region Windows Native HotKey Constants
            /// <summary>
            /// The Windows message id posted when a registered hot-key is
            /// activated.
            /// </summary>
            internal const int WM_HOTKEY = 0x0312;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The minimum valid hot-key id.
            /// </summary>
            internal const int HOTKEY_MIN = 0x0000;
            /// <summary>
            /// The maximum valid hot-key id.
            /// </summary>
            internal const int HOTKEY_MAX = 0xBFFF;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The ALT modifier flag for hot-key registration.
            /// </summary>
            internal const uint MOD_ALT = 0x0001;
            /// <summary>
            /// The CONTROL modifier flag for hot-key registration.
            /// </summary>
            internal const uint MOD_CONTROL = 0x0002;
            /// <summary>
            /// The SHIFT modifier flag for hot-key registration.
            /// </summary>
            internal const uint MOD_SHIFT = 0x0004;
            /// <summary>
            /// The Windows-key modifier flag for hot-key registration.
            /// </summary>
            internal const uint MOD_WIN = 0x0008; /* NOT USED */
            /// <summary>
            /// The no-repeat modifier flag for hot-key registration.
            /// </summary>
            internal const uint MOD_NOREPEAT = 0x4000;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Windows Native HotKey Methods
            /// <summary>
            /// Registers a global hot-key (User32 <c>RegisterHotKey</c>).
            /// </summary>
            /// <param name="handle">
            /// A handle to the window that receives hot-key messages.
            /// </param>
            /// <param name="id">
            /// The hot-key id.
            /// </param>
            /// <param name="modifiers">
            /// The modifier flags.
            /// </param>
            /// <param name="virtualKey">
            /// The virtual-key code.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            [DllImport(DllName.User32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool RegisterHotKey(
                IntPtr handle, int id, uint modifiers,
                uint virtualKey);

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Unregisters a global hot-key (User32 <c>UnregisterHotKey</c>).
            /// </summary>
            /// <param name="handle">
            /// A handle to the window the hot-key was registered against.
            /// </param>
            /// <param name="id">
            /// The hot-key id.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            [DllImport(DllName.User32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool UnregisterHotKey(
                IntPtr handle, int id);
            #endregion
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constants
#if NATIVE && WINDOWS
        /// <summary>
        /// The Windows message id used to detect hot-key activation.
        /// </summary>
        public const int MessageId = UnsafeNativeMethods.WM_HOTKEY;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The object used to synchronize access to this hot-key.
        /// </summary>
        private readonly object syncRoot = new object();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Static "Factory" Methods
        /// <summary>
        /// Creates a new hot-key associated with the specified manager,
        /// copying its form and window handle and assigning the next id.
        /// </summary>
        /// <param name="hotKeyManager">
        /// The manager that owns the new hot-key.
        /// </param>
        /// <param name="hotKey">
        /// An existing hot-key to copy non-identity state from, if any.
        /// </param>
        /// <param name="keys">
        /// The key combination for the new hot-key.
        /// </param>
        /// <param name="flags">
        /// The flags for the new hot-key.
        /// </param>
        /// <param name="text">
        /// The descriptive text for the new hot-key.
        /// </param>
        /// <returns>
        /// The created hot-key, or null on failure.
        /// </returns>
        public static IHotKey Create(
            IHotKeyManager hotKeyManager, /* in */
            IHotKey hotKey,               /* in */
            Keys keys,                    /* in */
            HotKeyFlags flags,            /* in */
            string text                   /* in */
            )
        {
            //
            // NOTE: We need the parent form, native window handle, and Id for
            //       the hot-key; however, these cannot be supplied directly by
            //       the user (i.e. via the hot-key editor).  Therefore, either
            //       try to get them from the existing hot-key being edited or
            //       from the hot-key manager currently in use.
            //
            Form form;
            IntPtr handle;
            int id;

            if (hotKey != null)
            {
                //
                // NOTE: Use the pre-existing hot-key as the basis for getting
                //       the remaining properties that we need to fully create
                //       another hot-key.
                //
                form = hotKey.Form;
                handle = hotKey.Handle;
                id = hotKey.Id;
            }
            else if (hotKeyManager != null)
            {
                //
                // NOTE: Use the hot-key manager as the basis for getting the
                //       remaining properties that we need to fully create
                //       another hot-key.
                //
                form = hotKeyManager.GetHotKeyManagerForm();
                handle = hotKeyManager.GetHotKeyHandle();
                id = hotKeyManager.GetNextHotKeyId();
            }
            else
            {
                //
                // NOTE: There is no pre-existing hot-key or hot-key manager;
                //       therefore, use the system default values (i.e. null
                //       and/or zero values).  The newly created hot-key will
                //       not function properly until the other properties are
                //       later set to something valid.
                //
                form = null;
                handle = IntPtr.Zero;
                id = 0;
            }

            return Create(form, handle, id, keys, flags, text);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new hot-key bound to the specified form, window handle,
        /// and id.
        /// </summary>
        /// <param name="form">
        /// The form that owns the new hot-key.
        /// </param>
        /// <param name="handle">
        /// The window handle the hot-key is registered against.
        /// </param>
        /// <param name="id">
        /// The id of the new hot-key.
        /// </param>
        /// <param name="keys">
        /// The key combination for the new hot-key.
        /// </param>
        /// <param name="flags">
        /// The flags for the new hot-key.
        /// </param>
        /// <param name="text">
        /// The descriptive text for the new hot-key.
        /// </param>
        /// <returns>
        /// The created hot-key.
        /// </returns>
        public static IHotKey Create(
            Form form,         /* in */
            IntPtr handle,     /* in */
            int id,            /* in */
            Keys keys,         /* in */
            HotKeyFlags flags, /* in */
            string text        /* in */
            )
        {
            return new HotKey(
                form, handle, id, keys, flags & ~HotKeyFlags.NonInstance,
                text);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="HotKey" /> instance with the specified
        /// identity, keys, flags, and text.
        /// </summary>
        /// <param name="form">
        /// The owning form.
        /// </param>
        /// <param name="handle">
        /// The window handle.
        /// </param>
        /// <param name="id">
        /// The hot-key id.
        /// </param>
        /// <param name="keys">
        /// The key combination.
        /// </param>
        /// <param name="flags">
        /// The hot-key flags.
        /// </param>
        /// <param name="text">
        /// The descriptive text.
        /// </param>
        private HotKey(
            Form form,         /* in */
            IntPtr handle,     /* in */
            int id,            /* in */
            Keys keys,         /* in */
            HotKeyFlags flags, /* in */
            string text        /* in */
            )
            : this(form, handle, id, keys, flags, text,
                   ReturnCode.Ok, null, 0, null, null)
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Constructs a new <see cref="HotKey" /> instance, also initializing
        /// its captured result and error state.
        /// </summary>
        /// <param name="form">
        /// The owning form.
        /// </param>
        /// <param name="handle">
        /// The window handle.
        /// </param>
        /// <param name="id">
        /// The hot-key id.
        /// </param>
        /// <param name="keys">
        /// The key combination.
        /// </param>
        /// <param name="flags">
        /// The hot-key flags.
        /// </param>
        /// <param name="text">
        /// The descriptive text.
        /// </param>
        /// <param name="code">
        /// The initial captured return code.
        /// </param>
        /// <param name="result">
        /// The initial captured result.
        /// </param>
        /// <param name="errorLine">
        /// The initial captured error line.
        /// </param>
        /// <param name="errorCode">
        /// The initial captured error code.
        /// </param>
        /// <param name="errorInfo">
        /// The initial captured error information.
        /// </param>
        private HotKey(
            Form form,         /* in */
            IntPtr handle,     /* in */
            int id,            /* in */
            Keys keys,         /* in */
            HotKeyFlags flags, /* in */
            string text,       /* in */
            ReturnCode code,   /* in */
            Result result,     /* in */
            int errorLine,     /* in */
            Result errorCode,  /* in */
            Result errorInfo   /* in */
            )
        {
            this.form = form;
            this.handle = handle;
            this.id = id;
            this.keys = keys;
            this.flags = flags;
            this.text = text;
            this.returnCode = code;
            this.result = result;
            this.errorLine = errorLine;
            this.errorCode = errorCode;
            this.errorInfo = errorInfo;

            ///////////////////////////////////////////////////////////////////

#if NATIVE && WINDOWS
            //
            // HACK: Silence a Mono C# compiler warning.
            //
            this.registered = false;
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHotKey Members
        #region Public Properties
        /// <summary>
        /// Gets the object used to synchronize access to this hot-key.
        /// </summary>
        public object SyncRoot
        {
            get { CheckDisposed(); return syncRoot; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Form" /> property.
        /// </summary>
        private Form form;
        /// <summary>
        /// Gets the form that owns this hot-key.
        /// </summary>
        public Form Form
        {
            get { CheckDisposed(); lock (syncRoot) { return form; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Handle" /> property.
        /// </summary>
        private IntPtr handle;
        /// <summary>
        /// Gets the window handle this hot-key is registered against.
        /// </summary>
        public IntPtr Handle
        {
            get { CheckDisposed(); lock (syncRoot) { return handle; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Id" /> property.
        /// </summary>
        private int id;
        /// <summary>
        /// Gets the integer id that identifies this hot-key.
        /// </summary>
        public int Id
        {
            get { CheckDisposed(); lock (syncRoot) { return id; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Keys" /> property.
        /// </summary>
        private Keys keys;
        /// <summary>
        /// Gets the full key combination (modifiers plus virtual key).
        /// </summary>
        public Keys Keys
        {
            get { CheckDisposed(); lock (syncRoot) { return keys; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the modifier keys portion of the key combination.
        /// </summary>
        public Keys Modifiers
        {
            get { CheckDisposed(); return GetModifiersForKeys(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the virtual (non-modifier) key portion of the key combination.
        /// </summary>
        public Keys VirtualKey
        {
            get { CheckDisposed(); return GetVirtualKeyForKeys(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Flags" /> property.
        /// </summary>
        private HotKeyFlags flags;
        /// <summary>
        /// Gets the flags that control this hot-key's behavior.
        /// </summary>
        public HotKeyFlags Flags
        {
            get { CheckDisposed(); lock (syncRoot) { return flags; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Text" /> property.
        /// </summary>
        private string text;
        /// <summary>
        /// Gets the descriptive text associated with this hot-key.
        /// </summary>
        public string Text
        {
            get { CheckDisposed(); lock (syncRoot) { return text; } }
        }

        ///////////////////////////////////////////////////////////////////////

#if NATIVE && WINDOWS
        /// <summary>
        /// The backing field for the <see cref="Registered" /> property.
        /// </summary>
        private bool registered;
#endif
        /// <summary>
        /// Gets a value indicating whether this hot-key is currently
        /// registered with the operating system.
        /// </summary>
        public bool Registered
        {
#if NATIVE && WINDOWS
            get { CheckDisposed(); lock (syncRoot) { return registered; } }
#else
            get { CheckDisposed(); return false; }
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="HitCount" /> property.
        /// </summary>
        private int hitCount;
        /// <summary>
        /// Gets the number of times this hot-key has been activated.
        /// </summary>
        public int HitCount
        {
            get { CheckDisposed(); lock (syncRoot) { return hitCount; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="HitTime" /> property.
        /// </summary>
        private DateTime? hitTime;
        /// <summary>
        /// Gets the time of the most recent activation, or null if it has
        /// never been activated.
        /// </summary>
        public DateTime? HitTime
        {
            get { CheckDisposed(); lock (syncRoot) { return hitTime; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="HitFlags" /> property.
        /// </summary>
        private HotKeyScriptFlags hitFlags;
        /// <summary>
        /// Gets the script flags in effect at the most recent activation.
        /// </summary>
        public HotKeyScriptFlags HitFlags
        {
            get { CheckDisposed(); lock (syncRoot) { return hitFlags; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="ReturnCode" /> property.
        /// </summary>
        private ReturnCode returnCode;
        /// <summary>
        /// Gets the return code captured from the most recent script
        /// evaluation.
        /// </summary>
        public ReturnCode ReturnCode
        {
            get { CheckDisposed(); lock (syncRoot) { return returnCode; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="Result" /> property.
        /// </summary>
        private Result result;
        /// <summary>
        /// Gets the result captured from the most recent script evaluation.
        /// </summary>
        public Result Result
        {
            get { CheckDisposed(); lock (syncRoot) { return result; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="ErrorLine" /> property.
        /// </summary>
        private int errorLine;
        /// <summary>
        /// Gets the error line number captured from the most recent script
        /// evaluation.
        /// </summary>
        public int ErrorLine
        {
            get { CheckDisposed(); lock (syncRoot) { return errorLine; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="ErrorCode" /> property.
        /// </summary>
        private Result errorCode;
        /// <summary>
        /// Gets the error code captured from the most recent script
        /// evaluation.
        /// </summary>
        public Result ErrorCode
        {
            get { CheckDisposed(); lock (syncRoot) { return errorCode; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The backing field for the <see cref="ErrorInfo" /> property.
        /// </summary>
        private Result errorInfo;
        /// <summary>
        /// Gets the error information captured from the most recent script
        /// evaluation.
        /// </summary>
        public Result ErrorInfo
        {
            get { CheckDisposed(); lock (syncRoot) { return errorInfo; } }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Notifies this hot-key that its owning form and window handle have
        /// changed.
        /// </summary>
        /// <param name="form">
        /// The new owning form.
        /// </param>
        /// <param name="handle">
        /// The new window handle.
        /// </param>
        public void ParentHasChanged(
            Form form,    /* in */
            IntPtr handle /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot)
            {
                this.form = form;
                this.handle = handle;
            }
        }

        ///////////////////////////////////////////////////////////////////////

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
        public string GetDisplayTag(
            string name /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot)
            {
                return ScriptOps.ExtractTag(text, name, 0);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Produces a list representation of this hot-key.
        /// </summary>
        /// <param name="full">
        /// Non-zero to include all fields; zero for the summary set.
        /// </param>
        /// <returns>
        /// A list describing this hot-key.
        /// </returns>
        public StringList ToList(
            bool full /* in */
            )
        {
            CheckDisposed();

            return ToList(HotKeyFlags.None, full, full, full, full);
        }

        ///////////////////////////////////////////////////////////////////////

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
        public StringList ToList(
            HotKeyFlags flagsMask, /* in */
            bool manager,          /* in */
            bool script,           /* in */
            bool other,            /* in */
            bool results           /* in */
            )
        {
            CheckDisposed();

            StringList list = new StringList();

            if (manager)
            {
                list.Add("form");
                list.Add((form != null) ? form.ToString() : null);

                list.Add("handle");
                list.Add(handle.ToString());
            }

            list.Add("id");
            list.Add(id.ToString());

            list.Add("keys");
            list.Add(WinFormsOps.GetKeysToShow(keys));

            HotKeyFlags newFlags = flags;

            if (flagsMask != HotKeyFlags.None)
                newFlags &= flagsMask;

            list.Add("flags");
            list.Add(newFlags.ToString());

            if (script)
            {
                list.Add("text");
                list.Add(text);
            }

            if (other)
            {
#if NATIVE && WINDOWS
                list.Add("registered");
                list.Add(registered.ToString());
#endif

                list.Add("hitCount");
                list.Add(hitCount.ToString());

                list.Add("hitTime");
                list.Add(LogOps.FormatHotKeyDateTime(hitTime));

                list.Add("hitFlags");
                list.Add(hitFlags.ToString());
            }

            if (results)
            {
                list.Add("returnCode");
                list.Add(returnCode.ToString());

                list.Add("result");
                list.Add(result);

                list.Add("errorLine");
                list.Add(errorLine.ToString());

                list.Add("errorCode");
                list.Add(errorCode);

                list.Add("errorInfo");
                list.Add(errorInfo);
            }

            return list;
        }

        ///////////////////////////////////////////////////////////////////////

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
        public bool HasFlags(
            HotKeyFlags hasFlags, /* in */
            bool all              /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot)
            {
                if (all)
                    return ((flags & hasFlags) == hasFlags);
                else
                    return ((flags & hasFlags) != HotKeyFlags.None);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the record that this hot-key was previously registered.
        /// </summary>
        public void ClearWasRegistered()
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    flags &= ~HotKeyFlags.WasRegistered;
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records that this hot-key was previously registered.
        /// </summary>
        public void SetWasRegistered()
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    flags |= HotKeyFlags.WasRegistered;
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Registers this hot-key with the operating system so it becomes
        /// active.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode Register(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (handle == IntPtr.Zero)
                    {
                        error = "invalid window handle";
                        return ReturnCode.Error;
                    }

#if NATIVE && WINDOWS
                    if ((id < UnsafeNativeMethods.HOTKEY_MIN) ||
                        (id > UnsafeNativeMethods.HOTKEY_MAX))
                    {
                        error = String.Format(
                            "hot-key id {0} is out-of-bounds", id);

                        return ReturnCode.Error;
                    }
#endif

                    if (id == 0) /* HACK: Not strictly required. */
                    {
                        error = String.Format(
                            "invalid hot-key id {0}", id);

                        return ReturnCode.Error;
                    }

#if NATIVE && WINDOWS
                    if (registered)
                    {
                        error = String.Format(
                            "hot-key {0} already registered", id);

                        return ReturnCode.Error;
                    }
#endif

                    if (form == null)
                    {
                        error = "invalid form";
                        return ReturnCode.Error;
                    }

#if NATIVE && WINDOWS
                    uint modifiers = GetNativeModifiers();
                    uint virtualKey = GetNativeVirtualKey();
#endif
                    ReturnCode code = ReturnCode.Ok;
                    Result localError = null;

                    form.Invoke(new DelegateWithNoArgs(delegate()
                    {
#if NATIVE && WINDOWS
                        if (UnsafeNativeMethods.RegisterHotKey(
                                handle, id, modifiers, virtualKey))
                        {
                            registered = true;
                        }
                        else
                        {
                            int lastError = Marshal.GetLastWin32Error();

                            localError = String.Format(
                                "RegisterHotKey({1}) failed with error {0}: {2}",
                                lastError, id, Utility.GetErrorMessage(lastError));

                            code = ReturnCode.Error;
                        }
#else
                        localError = "not implemented";
                        code = ReturnCode.Error;
#endif
                    }));

#if NATIVE && WINDOWS
                    if (registered)
                    {
                        LogOrComplain(null,
                            LogOps.FormatHotKeyRegistrationLogEntry(this));
                    }
#endif

                    if (code != ReturnCode.Ok)
                        error = localError;

                    return code;
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Unregisters this hot-key from the operating system so it is no
        /// longer active.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public ReturnCode Unregister(
            ref Result error /* out */
            )
        {
            CheckDisposed();

            try
            {
                lock (syncRoot)
                {
                    if (handle == IntPtr.Zero)
                    {
                        error = "invalid window handle";
                        return ReturnCode.Error;
                    }

#if NATIVE && WINDOWS
                    if ((id < UnsafeNativeMethods.HOTKEY_MIN) ||
                        (id > UnsafeNativeMethods.HOTKEY_MAX))
                    {
                        error = String.Format(
                            "hot-key id {0} is out-of-bounds", id);

                        return ReturnCode.Error;
                    }
#endif

                    if (id == 0) /* HACK: Not strictly required. */
                    {
                        error = String.Format(
                            "invalid hot-key id {0}", id);

                        return ReturnCode.Error;
                    }

#if NATIVE && WINDOWS
                    if (!registered)
                    {
                        error = String.Format(
                            "hot-key {0} not registered", id);

                        return ReturnCode.Error;
                    }
#endif

                    if (form == null)
                    {
                        error = "invalid form";
                        return ReturnCode.Error;
                    }

                    ReturnCode code = ReturnCode.Ok;
                    Result localError = null;

                    form.Invoke(new DelegateWithNoArgs(delegate()
                    {
#if NATIVE && WINDOWS
                        if (UnsafeNativeMethods.UnregisterHotKey(handle, id))
                        {
                            registered = false;
                        }
                        else
                        {
                            int lastError = Marshal.GetLastWin32Error();

                            localError = String.Format(
                                "UnregisterHotKey({1}) failed with error {0}: {2}",
                                lastError, id, Utility.GetErrorMessage(lastError));

                            code = ReturnCode.Error;
                        }
#else
                        localError = "not implemented";
                        code = ReturnCode.Error;
#endif
                    }));

#if NATIVE && WINDOWS
                    if (!registered)
                    {
                        LogOrComplain(null,
                            LogOps.FormatHotKeyRegistrationLogEntry(this));
                    }
#endif

                    if (code != ReturnCode.Ok)
                        error = localError;

                    return code;
                }
            }
            finally
            {
                /* NO RESULT */
                ScriptOps.NotifyViewForms();
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the captured return code, result, and error information from
        /// the most recent script evaluation.
        /// </summary>
        public void ResetResult()
        {
            CheckDisposed();

            lock (syncRoot)
            {
                result = null;
                returnCode = ReturnCode.Ok;
                errorLine = 0;
                errorCode = null;
                errorInfo = null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Evaluates this hot-key's associated script, capturing its outcome.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter in which to evaluate the script.
        /// </param>
        /// <param name="flags">
        /// The script flags controlling how the script is evaluated.
        /// </param>
        public void EvaluateScript(
            Interpreter interpreter, /* in */
            HotKeyScriptFlags flags  /* in */
            )
        {
            CheckDisposed();

            if (ScriptOps.HasFlags(
                    flags, HotKeyScriptFlags.RecordAsHit, true))
            {
                RecordAsHit(flags);
            }

            Interpreter newInterpreter = interpreter;
            bool safe = ScriptOps.IsSafe(newInterpreter);
            bool asynchronous = HasFlags(HotKeyFlags.Asynchronous, true);

            try
            {
                bool isolated = HasFlags(HotKeyFlags.Isolated, true);

                if (isolated)
                {
                    CreateFlags createFlags = CreateFlags.SingleUse;

                    if (HasFlags(HotKeyFlags.Safe, true))
                        createFlags |= CreateFlags.SafeAndHideUnsafe;

                    Result localResult = null;

                    newInterpreter = Interpreter.Create(
                        null, createFlags, HostCreateFlags.SingleUse,
                        ref localResult);

                    safe = ScriptOps.IsSafe(newInterpreter);

                    if (newInterpreter == null)
                    {
                        lock (syncRoot)
                        {
                            result = localResult;
                            returnCode = ReturnCode.Error;
                        }

                        //
                        // NOTE: Figure out if the corresponding logging
                        //       flag is enabled; if so, log the creation
                        //       error just as though it were a script
                        //       evaluation error.
                        //
                        if (IsLoggingEnabled(asynchronous))
                            LogResultOrComplain(newInterpreter);

                        return;
                    }
                    else
                    {
                        MaybeLogInterpreter(
                            "CREATE", newInterpreter, asynchronous, safe);
                    }
                }

                if (newInterpreter != null)
                {
                    ReturnCode localCode;
                    Result localResult = null;

                    if (asynchronous)
                    {
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                        if (Shell.Form.IsHotKeyIsolated(newInterpreter))
                        {
                            AsynchronousCallbackBridge asynchronousCallbackBridge =
                                AsynchronousCallbackBridge.Create(this, ref localResult);

                            if (asynchronousCallbackBridge != null)
                            {
                                if (HasFlags(HotKeyFlags.ResetCancel, true))
                                    ScriptOps.ResetCancel(newInterpreter);

                                localCode = newInterpreter.EvaluateScript(text,
                                    asynchronousCallbackBridge.AsynchronousCallback,
                                    new ClientData(isolated), ref localResult);

                                if (localCode != ReturnCode.Ok)
                                {
                                    lock (syncRoot)
                                    {
                                        result = localResult;
                                        returnCode = localCode;
                                    }
                                }
                            }
                            else
                            {
                                lock (syncRoot)
                                {
                                    result = localResult;
                                    returnCode = ReturnCode.Error;
                                }
                            }
                        }
                        else
#endif
                        {
                            if (HasFlags(HotKeyFlags.ResetCancel, true))
                                ScriptOps.ResetCancel(newInterpreter);

                            localCode = newInterpreter.EvaluateScript(text,
                                AsynchronousCallback, new ClientData(isolated),
                                ref localResult);

                            if (localCode != ReturnCode.Ok)
                            {
                                lock (syncRoot)
                                {
                                    result = localResult;
                                    returnCode = localCode;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (HasFlags(HotKeyFlags.ResetCancel, true))
                            ScriptOps.ResetCancel(newInterpreter);

                        int localErrorLine = 0;

                        localCode = newInterpreter.EvaluateScript(
                            text, ref localResult, ref localErrorLine);

                        lock (syncRoot)
                        {
                            returnCode = localCode;
                            result = localResult;
                            errorLine = localErrorLine;
                        }

                        if (localCode == ReturnCode.Error)
                        {
                            ErrorCodeAndInfoFromInterpreterOrComplain(
                                newInterpreter);
                        }
                    }
                }
                else
                {
                    lock (syncRoot)
                    {
                        result = "invalid interpreter";
                        returnCode = ReturnCode.Error;
                    }
                }

                ///////////////////////////////////////////////////////////////

                if (IsLoggingEnabled(false))
                    LogResultOrComplain(newInterpreter);
            }
            finally
            {
                if (!asynchronous)
                {
#if WINFORMS
                    MaybeUseKeyEventManager(newInterpreter);
#endif

                    if (!Object.ReferenceEquals(newInterpreter, interpreter))
                    {
                        ReturnCode disposeCode;
                        Result disposeError = null;

                        disposeCode = Utility.TryDisposeObject<Interpreter>(
                            ref newInterpreter, ref disposeError);

                        if (disposeCode == ReturnCode.Ok)
                        {
                            MaybeLogInterpreter(
                                "DISPOSE", newInterpreter, asynchronous,
                                safe);
                        }
                        else
                        {
                            LogOps.Complain(
                                this, newInterpreter, disposeCode,
                                disposeError);
                        }
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

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
        public void SetErrorCodeAndInfo(
            Result errorCode, /* in */
            Result errorInfo  /* in */
            )
        {
            CheckDisposed();

            lock (syncRoot)
            {
                this.errorCode = errorCode;
                this.errorInfo = errorInfo;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Copies this hot-key's captured return code and result into the
        /// specified interpreter and the supplied reference arguments.
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
        public void ResultToInterpreter(
            Interpreter interpreter,   /* in */
            ref ReturnCode returnCode, /* out */
            ref Result result          /* out */
            )
        {
            CheckDisposed();

            lock (syncRoot)
            {
                result = this.result;
                returnCode = this.returnCode;

                if ((this.returnCode == ReturnCode.Error) &&
                    (interpreter != null))
                {
                    interpreter.ErrorLine = errorLine;

                    ReturnCode toCode;
                    Result toError = null;

                    toCode = ErrorCodeAndInfoToInterpreter(
                        interpreter, ref toError);

                    if (toCode != ReturnCode.Ok)
                        LogOps.Complain(this, interpreter, toCode, toError);
                }
            }
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IAsynchronousCallback Members
        /// <summary>
        /// The asynchronous callback invoked when an asynchronous script
        /// evaluation completes, applying its captured outcome.
        /// </summary>
        /// <param name="context">
        /// The context describing the completed evaluation.
        /// </param>
        public void Invoke(
            IAsynchronousContext context /* in */
            )
        {
            CheckDisposed();

            AsynchronousCallback(context);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Gets the modifier keys derived from this hot-key's key combination.
        /// </summary>
        /// <returns>
        /// The modifier keys.
        /// </returns>
        private Keys GetModifiersForKeys()
        {
            lock (syncRoot)
            {
                return WinFormsOps.GetModifiers(keys);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the virtual key derived from this hot-key's key combination.
        /// </summary>
        /// <returns>
        /// The virtual key.
        /// </returns>
        private Keys GetVirtualKeyForKeys()
        {
            lock (syncRoot)
            {
                return WinFormsOps.GetVirtualKey(keys);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the native modifier flags corresponding to this hot-key's
        /// modifiers.
        /// </summary>
        /// <returns>
        /// The native modifier flags.
        /// </returns>
        private uint GetNativeModifiers()
        {
            uint result = 0;

#if NATIVE && WINDOWS
            Keys modifiers = GetModifiersForKeys();

            if ((modifiers & Keys.Alt) == Keys.Alt)
                result |= UnsafeNativeMethods.MOD_ALT;

            if ((modifiers & Keys.Control) == Keys.Control)
                result |= UnsafeNativeMethods.MOD_CONTROL;

            if ((modifiers & Keys.Shift) == Keys.Shift)
                result |= UnsafeNativeMethods.MOD_SHIFT;

            if (HasFlags(HotKeyFlags.NoRepeat, true))
                result |= UnsafeNativeMethods.MOD_NOREPEAT;
#endif

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the native virtual-key code corresponding to this hot-key's
        /// virtual key.
        /// </summary>
        /// <returns>
        /// The native virtual-key code.
        /// </returns>
        private uint GetNativeVirtualKey()
        {
            return (uint)GetVirtualKeyForKeys();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Records this hot-key as having been activated now, updating its hit
        /// count, time, and flags.
        /// </summary>
        /// <param name="flags">
        /// The script flags in effect at activation.
        /// </param>
        private void RecordAsHit(
            HotKeyScriptFlags flags /* in */
            )
        {
            lock (syncRoot)
            {
                Interlocked.Increment(ref hitCount); /* REDUNDANT */

                hitTime = Utility.GetNow();
                hitFlags = flags;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Captures the error code and error information from the interpreter
        /// into this hot-key.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to read the error state from.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private ReturnCode ErrorCodeAndInfoFromInterpreter(
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            return ScriptOps.ErrorCodeAndInfoFromInterpreter(
                this, interpreter, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the error code and error information from this hot-key
        /// into the interpreter.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to write the error state to.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private ReturnCode ErrorCodeAndInfoToInterpreter(
            Interpreter interpreter, /* in */
            ref Result error         /* out */
            )
        {
            return ScriptOps.ErrorCodeAndInfoToInterpreter(
                this, interpreter, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Captures the error code and error information from the interpreter
        /// into this hot-key, complaining on failure rather than returning an
        /// error.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to read the error state from.
        /// </param>
        private void ErrorCodeAndInfoFromInterpreterOrComplain(
            Interpreter interpreter /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = ErrorCodeAndInfoFromInterpreter(interpreter, ref error);

            if (code != ReturnCode.Ok)
                LogOps.Complain(this, interpreter, code, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether result logging is enabled for this hot-key,
        /// given whether the evaluation was asynchronous.
        /// </summary>
        /// <param name="asynchronous">
        /// Non-zero when the evaluation was asynchronous.
        /// </param>
        /// <returns>
        /// Non-zero when logging is enabled; otherwise, zero.
        /// </returns>
        private bool IsLoggingEnabled(
            bool? asynchronous /* in */
            )
        {
            HotKeyFlags hasFlags;

            //
            // NOTE: In this context, null means "automatically detect" if
            //       the hot-key is asynchronous.
            //
            if (asynchronous == null)
            {
                if (HasFlags(HotKeyFlags.Asynchronous, true))
                    hasFlags = HotKeyFlags.LogAsynchronous;
                else
                    hasFlags = HotKeyFlags.LogSynchronous;
            }
            else if ((bool)asynchronous)
            {
                hasFlags = HotKeyFlags.LogAsynchronous;
            }
            else
            {
                hasFlags = HotKeyFlags.LogSynchronous;
            }

            return HasFlags(hasFlags, true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text to the hot-key log, complaining on
        /// failure.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry.
        /// </param>
        /// <param name="text">
        /// The text to log.
        /// </param>
        private void LogOrComplain(
            Interpreter interpreter, /* in */
            string text              /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = Shell.Form.AppendToHotKeyLog(
                interpreter, text, ref error);

            if (code != ReturnCode.Ok)
                LogOps.Complain(this, interpreter, code, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends the supplied text to the hot-key log when logging is
        /// enabled for this hot-key, complaining on failure.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry.
        /// </param>
        /// <param name="text">
        /// The text to log.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero when the evaluation was asynchronous.
        /// </param>
        private void MaybeLogOrComplain(
            Interpreter interpreter, /* in */
            string text,             /* in */
            bool? asynchronous       /* in */
            )
        {
            if (IsLoggingEnabled(asynchronous))
                LogOrComplain(interpreter, text);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logs this hot-key's most recent result, complaining on failure.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter associated with the log entry.
        /// </param>
        private void LogResultOrComplain(
            Interpreter interpreter /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = Shell.Form.LogHotKeyResult(interpreter, this, ref error);

            if (code != ReturnCode.Ok)
                LogOps.Complain(this, interpreter, code, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logs a message describing the temporary interpreter used to
        /// evaluate this hot-key's script, when logging is enabled.
        /// </summary>
        /// <param name="operation">
        /// The operation the interpreter was created for.
        /// </param>
        /// <param name="interpreter">
        /// The temporary interpreter being described.
        /// </param>
        /// <param name="asynchronous">
        /// Non-zero when the evaluation was asynchronous.
        /// </param>
        /// <param name="safe">
        /// Non-zero when the interpreter is safe.
        /// </param>
        private void MaybeLogInterpreter(
            string operation,        /* in */
            Interpreter interpreter, /* in */
            bool? asynchronous,      /* in */
            bool safe                /* in */
            )
        {
            if (IsLoggingEnabled(asynchronous))
            {
                LogOrComplain(interpreter, String.Format(
                    "{0}: temporary {1}isolated interpreter {2}",
                    operation, safe ? "safe " : String.Empty,
                    HotKeyOps.ToString(interpreter)));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied asynchronous context indicates the
        /// evaluation ran in an isolated interpreter.
        /// </summary>
        /// <param name="context">
        /// The asynchronous context to examine.
        /// </param>
        /// <returns>
        /// Non-zero when the evaluation was isolated; otherwise, zero.
        /// </returns>
        private static bool WasIsolated(
            IAsynchronousContext context /* in */
            )
        {
            if (context != null)
            {
                IClientData clientData = context.ClientData;

                if ((clientData != null) && (clientData.Data is bool))
                    return (bool)clientData.Data;
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The asynchronous callback bridged across application domains that
        /// applies the completed evaluation's result to this hot-key.
        /// </summary>
        /// <param name="context">
        /// The context describing the completed evaluation.
        /// </param>
        private void AsynchronousCallback(
            IAsynchronousContext context /* in */
            ) /* AsynchronousCallback */
        {
            if (context == null)
                return;

            Interpreter interpreter = context.Interpreter;
            bool safe = ScriptOps.IsSafe(interpreter);

            try
            {
                lock (syncRoot)
                {
                    returnCode = context.ReturnCode;
                    result = context.Result;
                    errorLine = context.ErrorLine;

                    ///////////////////////////////////////////////////////////

                    if (interpreter != null)
                        ErrorCodeAndInfoFromInterpreterOrComplain(interpreter);
                }

                ///////////////////////////////////////////////////////////////

                if (IsLoggingEnabled(true))
                    LogResultOrComplain(interpreter);
            }
            finally
            {
#if WINFORMS
                MaybeUseKeyEventManager(interpreter);
#endif

                if (WasIsolated(context))
                {
                    ReturnCode disposeCode;
                    Result disposeError = null;

                    disposeCode = Utility.TryDisposeObject<Interpreter>(
                        ref interpreter, ref disposeError);

                    if (disposeCode == ReturnCode.Ok)
                    {
                        MaybeLogInterpreter(
                            "DISPOSE", interpreter, true, safe);
                    }
                    else
                    {
                        LogOps.Complain(
                            this, interpreter, disposeCode, disposeError);
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

#if WINFORMS
        /// <summary>
        /// Notifies the key-event manager (the interpreter) of the key being
        /// hit, when this hot-key requests it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to notify.
        /// </param>
        private void MaybeUseKeyEventManager(
            Interpreter interpreter /* in */
            )
        {
            if (interpreter == null)
                return;

            if (!HasFlags(HotKeyFlags.KeyEventManager, true))
                return;

            int count = 0;
            ReturnCode code;
            Result error = null;

            code = interpreter.FireKeyEventHandlers(
                EventType.KeyUp, interpreter,
                new KeyEventArgs(keys), ref count,
                ref error);

            if (code != ReturnCode.Ok)
            {
                MaybeLogOrComplain(interpreter, String.Format(
                    "MaybeUseKeyEventManager: count = {0}, " +
                    "code = {1}, error = {2}", count, code,
                    Utility.FormatWrapOrNull(error)), null);
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region System.Object Overrides
        /// <summary>
        /// Returns a string that represents this hot-key.
        /// </summary>
        /// <returns>
        /// A string that represents this hot-key.
        /// </returns>
        public override string ToString()
        {
            CheckDisposed();

            StringList list = ToList(true);

            return (list != null) ? list.ToString() : String.Empty;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ICloneable Members
        /// <summary>
        /// Creates a copy of this hot-key.
        /// </summary>
        /// <returns>
        /// A new copy of this hot-key.
        /// </returns>
        public object Clone() /* DEEP COPY */
        {
            CheckDisposed();

            return new HotKey(
                form, handle, id, keys, flags, text, returnCode,
                result, errorLine, errorCode, errorInfo);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Destructor
        ~HotKey()
        {
            Dispose(false);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IMaybeDisposed Members
        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        public bool Disposed
        {
            get
            {
                // CheckDisposed(); /* EXEMPT */

                lock (syncRoot)
                {
                    return disposed;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a value indicating whether this instance is being disposed.
        /// </summary>
        public bool Disposing
        {
            get
            {
                // CheckDisposed(); /* EXEMPT */

                throw new NotImplementedException();
            }
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
            if (disposed && Engine.IsThrowOnDisposed(null, false))
                throw new ObjectDisposedException(typeof(HotKey).Name);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        /// <param name="disposing">
        /// Non-zero if this method is being called from <see
        /// cref="IDisposable.Dispose" />; zero if it is being called from the
        /// finalizer.
        /// </param>
        private /* protected virtual */ void Dispose(
            bool disposing /* in */
            )
        {
            lock (syncRoot)
            {
                if (!disposed)
                {
                    //if (disposing)
                    //{
                    //    ////////////////////////////////////
                    //    // dispose managed resources here...
                    //    ////////////////////////////////////
                    //}

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////

#if NATIVE && WINDOWS
                    if (registered)
                    {
                        ReturnCode unregisterCode;
                        Result unregisterError = null;

                        try
                        {
                            unregisterCode = Unregister(
                                ref unregisterError); /* throw */
                        }
                        catch (Exception e)
                        {
                            unregisterError = e;
                            unregisterCode = ReturnCode.Error;
                        }

                        if (unregisterCode != ReturnCode.Ok)
                        {
                            LogOps.Complain(
                                this, null, unregisterCode, unregisterError);
                        }
                    }
#endif

                    //
                    // NOTE: This object is now disposed.
                    //
                    disposed = true;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
