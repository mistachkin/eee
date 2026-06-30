/*
 * HotKeyOps.cs --
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
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using HotKey.Interfaces.Private;
using Eagle._Interfaces.Public;

namespace HotKey.Components.Private
{
    #region Hot-Key Flags Enumeration
    /// <summary>
    /// Flags that control the behavior of a hot-key, including how its script
    /// is evaluated (synchronous/asynchronous, isolated, safe), logging and
    /// complaint behavior, registration options, key-event-manager
    /// notification, and which flags are shown in the viewer.
    /// </summary>
    [Flags()]
    [ObjectId("60cc5419-3778-48c7-826f-80d31fcf8bad")]
    internal enum HotKeyFlags
    {
        /// <summary>
        /// No special treatment.
        /// </summary>
        None = 0x0,               /* No special treatment.*/

        /// <summary>
        /// Invalid flag; do not use.
        /// </summary>
        Invalid = 0x1,            /* Invalid flag, do not use. */

        /// <summary>
        /// Prevent complaints from the hot-key class.
        /// </summary>
        NoComplain = 0x2,         /* Prevent complaints from HotKey class. */

        /// <summary>
        /// Prevent resetting the result before evaluation.
        /// </summary>
        NoResetResult = 0x4,      /* Prevent resets of result before eval. */

        /// <summary>
        /// Evaluate the associated script asynchronously.
        /// </summary>
        Asynchronous = 0x8,       /* Evaluate the associated script async. */

        /// <summary>
        /// Log the synchronous script result to the manager.
        /// </summary>
        LogSynchronous = 0x10,    /* Log sync script result to the manager. */

        /// <summary>
        /// Log the asynchronous script result to the manager.
        /// </summary>
        LogAsynchronous = 0x20,   /* Log async script result to the manager. */

        /// <summary>
        /// The hot-key was registered when last saved.
        /// </summary>
        WasRegistered = 0x40,     /* Was registered when last saved. */

        /// <summary>
        /// The key will be fully handled by the plugin.
        /// </summary>
        FullyHandled = 0x80,      /* Key will be fully handled by us. */

        /// <summary>
        /// Prevent logging of "key hit" messages.
        /// </summary>
        NoLogHit = 0x100,         /* Prevent logging of "key hit" messages. */

        /// <summary>
        /// Prevent logging of errors and exceptions.
        /// </summary>
        NoLogError = 0x200,       /* Prevent logging of errors/exceptions. */

        /// <summary>
        /// Reset the script cancellation flag before evaluation.
        /// </summary>
        ResetCancel = 0x400,      /* Reset script cancellation flag? */

        /// <summary>
        /// Use a fresh (isolated) interpreter for evaluation.
        /// </summary>
        Isolated = 0x800,         /* Use a fresh interpreter for evaluation. */

        /// <summary>
        /// Use a "safe" interpreter for evaluation.
        /// </summary>
        Safe = 0x1000,            /* Use a "safe" interpreter for evaluation. */

        /// <summary>
        /// Use MOD_NOREPEAT when registering the hot-key.
        /// </summary>
        NoRepeat = 0x2000,        /* Use MOD_NOREPEAT when registering. */

        ///////////////////////////////////////////////////////////////////////

#if WINFORMS
        /// <summary>
        /// Always notify the associated key-event manager (the interpreter) of
        /// the key being hit after evaluation.
        /// </summary>
        KeyEventManager = 0x4000, /* Always notify the associated key event
                                   * manager (i.e. the interpreter) of the
                                   * key being hit after evaluation. */

        /// <summary>
        /// The key-event-manager notification, combined with the "maybe"
        /// marker.
        /// </summary>
        MaybeKeyEventManager = KeyEventManager | ForMaybe,
#else
        /// <summary>
        /// The "maybe" key-event-manager value when WinForms is unavailable
        /// (no notification).
        /// </summary>
        MaybeKeyEventManager = None | ForMaybe,
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom0 = 0x8000,       /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom1 = 0x10000,      /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom2 = 0x20000,      /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom3 = 0x40000,      /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom4 = 0x80000,      /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom5 = 0x100000,     /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom6 = 0x200000,     /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom7 = 0x400000,     /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom8 = 0x800000,     /* Reserved for application/user use. */

        /// <summary>
        /// Reserved for application or user use.
        /// </summary>
        Custom9 = 0x1000000,    /* Reserved for application/user use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reserved for use with other "Maybe" flag masks.
        /// </summary>
        ForMaybe = 0x40000000,  /* Reserved for use with other "Maybe"
                                 * flag masks. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        Reserved = unchecked((int)0x80000000), /* Reserved for future use. */

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These flags are always masked off during the instance
        //       creation process.
        //
        /// <summary>
        /// The flags that are always masked off during instance creation.
        /// </summary>
        NonInstance = Invalid,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These are the flags displayed by the viewer interface.
        //
        /// <summary>
        /// The flags that are displayed by the viewer interface.
        /// </summary>
        ViewMask = NoComplain | NoResetResult | Asynchronous |
                   LogSynchronous | LogAsynchronous | WasRegistered |
                   FullyHandled | NoLogHit | NoLogError | ResetCancel |
                   Isolated | Safe | NoRepeat | MaybeKeyEventManager,

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: The default flags for newly created hot-keys.
        //
        /// <summary>
        /// The default flags for newly created hot-keys.
        /// </summary>
        Default = Invalid
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Hot-Key EventType Enumeration
    /// <summary>
    /// Identifies the kinds of hook points at which the hot-key manager can
    /// run a hook script (for example, when the manager is about to close).
    /// </summary>
    [Flags()]
    [ObjectId("42ac693d-1259-4366-a8cb-1b15098154f9")]
    internal enum HotKeyHookType : ulong
    {
        /// <summary>
        /// No special treatment.
        /// </summary>
        None = 0x0,               /* No special treatment.*/

        /// <summary>
        /// Invalid flag; do not use.
        /// </summary>
        Invalid = 0x1,            /* Invalid flag, do not use. */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The manager wants to close; the hook may veto it.
        /// </summary>
        OnManagerClose = 0x100,   /* The manager wants to close.
                                   * Maybe don't let it? */

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Marker bit indicating the default set of hook types.
        /// </summary>
        ForDefault = 0x10000000,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The mask of the base hook types.
        /// </summary>
        BaseTypeMask = OnManagerClose,

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default set of hook types.
        /// </summary>
        Default = OnManagerClose | ForDefault
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Provides hot-key helper methods, including formatting the plugin
    /// "about" text (with the text-editor version), describing an interpreter,
    /// and parsing a hot-key from its dictionary string representation.
    /// </summary>
    [ObjectId("d1d51ba7-d79b-4549-b56b-3a1370c425f9")]
    internal static class HotKeyOps
    {
        #region Public Constants
        //
        // NOTE: This is the name of an optional script variable that can be
        //       used to prevent the plugin thread from being started by the
        //       plugin instance via the Initialize method.
        //
        /// <summary>
        /// The name of an optional script variable that, when present,
        /// prevents the plugin from starting the hot-key manager thread during
        /// initialization.
        /// </summary>
        public static readonly string NoThreadVariableName =
            "::" + typeof(Enterprise).FullName + "_NoThread";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a human-readable description of the text-editor implementation
        /// in use (Scintilla, when compiled with it, or the Windows Forms text
        /// box), including its version.
        /// </summary>
        /// <returns>
        /// The text-editor version description.
        /// </returns>
        private static string GetTextEditorVersion()
        {
            Assembly assembly;
            string prefix;
            string suffix;
            string name;

#if SCINTILLA
#if SCINTILLA_30
            prefix = null;
#else
            prefix = "Legacy ";
#endif
            suffix = ScintillaOps.GetNativeLibraryVersion();

            if (!String.IsNullOrEmpty(suffix))
                suffix = String.Format(", Scintilla v{0}", suffix);

            assembly = typeof(ScintillaNET.Scintilla).Assembly;
            name = typeof(ScintillaNET.Scintilla).Namespace;
#else
            prefix = "Windows Forms ";
            suffix = null;
            assembly = typeof(TextBoxBase).Assembly;
            name = typeof(TextBoxBase).Name;
#endif

            return String.Format("{0}{1} v{2}{3}",
                prefix, name, Utility.GetAssemblyVersion(assembly),
                suffix);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Formats the "about" text for the specified plugin, appending the
        /// text-editor version on its own line.
        /// </summary>
        /// <param name="plugin">
        /// The plugin whose about text is formatted.
        /// </param>
        /// <returns>
        /// The formatted about text.
        /// </returns>
        public static string FormatPluginAbout(
            IPlugin plugin /* in */
            )
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(Utility.FormatPluginAbout(plugin, false));

            int baseLength = builder.Length - 1;
            string version = GetTextEditorVersion();

            if (version != null)
            {
                int versionLength = version.Length;

                if (baseLength < versionLength)
                    baseLength = versionLength;
            }

            if (!String.IsNullOrEmpty(version))
            {
                if (builder.Length > 0)
                {
                    builder.Append(Environment.NewLine);
                    builder.Append(Characters.HorizontalTab);
                    builder.Append(Characters.MinusSign, baseLength);
                }

                builder.Append(Environment.NewLine);
                builder.Append(Characters.HorizontalTab);
                builder.Append(version);
            }

            return builder.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a description of the specified interpreter that is safe to
        /// use even when the interpreter has been disposed (falling back to
        /// its id in that case).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to describe, which may be null or disposed.
        /// </param>
        /// <returns>
        /// A description of the interpreter.
        /// </returns>
        public static string ToString(
            Interpreter interpreter /* in */
            )
        {
            //
            // NOTE: This may be a bit too verbose.  In production code, the
            //       "right" thing to do here would be to use the "IdNoThrow"
            //       property IF there is any chance the interpreter might be
            //       disposed; however, for this particular method only, the
            //       caller(s) do not want to be bothered to check for a null
            //       interpreter either.  Therefore, this method is correct,
            //       even if a bit weird looking.
            //
            if ((interpreter == null) || !interpreter.Disposed)
                return Utility.FormatWrapOrNull(interpreter);
            else
                return Utility.FormatWrapOrNull(interpreter.IdNoThrow);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses a hot-key from its dictionary string representation (an
        /// even-length list of name/value pairs for id, keys, flags, and
        /// text) and creates a new hot-key bound to the supplied form and
        /// window handle.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to split and parse the value.
        /// </param>
        /// <param name="form">
        /// The form that will own the created hot-key.
        /// </param>
        /// <param name="handle">
        /// The window handle the hot-key will be registered against.
        /// </param>
        /// <param name="value">
        /// The dictionary string describing the hot-key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The created hot-key, or null on failure.
        /// </returns>
        public static IHotKey GetFromString(
            Interpreter interpreter, /* in */
            Form form,               /* in */
            IntPtr handle,           /* in */
            string value,            /* in */
            ref Result error         /* out */
            )
        {
            ReturnCode code;
            StringList list = null;

            code = Parser.SplitList(
                interpreter, value, 0, Length.Invalid, false, ref list,
                ref error);

            if (code != ReturnCode.Ok)
                return null;

            if ((list.Count % 2) != 0)
            {
                error = "dictionary must have an even number of elements";
                return null;
            }

            CultureInfo cultureInfo = (interpreter != null) ?
                interpreter.CultureInfo : null;

            int id = 0;
            Keys keys = Keys.None;
            HotKeyFlags flags = HotKeyFlags.Default;
            string text = null;

            for (int index = 0; index < list.Count; index += 2)
            {
                if (Utility.SystemStringEquals(list[index], "id"))
                {
                    code = Value.GetInteger2(
                        list[index + 1], ValueFlags.AnyInteger,
                        cultureInfo, ref id, ref error);

                    if (code != ReturnCode.Ok)
                        return null;
                }
                else if (Utility.SystemStringEquals(list[index], "keys"))
                {
                    object enumValue = Utility.TryParseFlagsEnum(
                        interpreter, typeof(Keys), keys.ToString(),
                        list[index + 1], cultureInfo, true, true,
                        true, ref error);

                    if (!(enumValue is Keys))
                        return null;
                }
                else if (Utility.SystemStringEquals(list[index], "flags"))
                {
                    object enumValue = Utility.TryParseFlagsEnum(
                        interpreter, typeof(HotKeyFlags), flags.ToString(),
                        list[index + 1], cultureInfo, true, true, true,
                        ref error);

                    if (!(enumValue is HotKeyFlags))
                        return null;
                }
                else if (Utility.SystemStringEquals(list[index], "text"))
                {
                    text = list[index + 1];
                }
            }

            return HotKey.Create(form, handle, id, keys, flags, text);
        }
    }
}
