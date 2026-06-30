/*
 * HotKeyViewForm.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using HotKey.Components.Private;
using HotKey.Interfaces.Private;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace HotKey.Forms
{
    /// <summary>
    /// Implements the hot-key viewer form, which lists all defined hot-keys in
    /// a checked list and lets the user add, remove, edit, register, save,
    /// load, and evaluate them, view the log, and run an interactive loop.
    /// </summary>
    [ObjectId("00c0aee7-6ff4-4074-81b6-6062b0fcf9c7")]
    internal sealed partial class HotKeyViewForm : BaseForm, IHotKeyViewer
    {
        #region Private Hot-Key List Item Class
        /// <summary>
        /// Represents a single hot-key entry in the viewer's checked list,
        /// wrapping the hot-key and its display text.
        /// </summary>
        [ObjectId("6505bec7-26d7-4369-bfda-fd767429feca")]
        private sealed class HotKeyListItem
        {
            #region Private Data
            /// <summary>
            /// The hot-key represented by this list item.
            /// </summary>
            private IHotKey hotKey;
            /// <summary>
            /// Non-zero when the item is shown in advanced (detailed) form.
            /// </summary>
            private bool advanced;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new list item wrapping the specified hot-key.
            /// </summary>
            /// <param name="hotKey">
            /// The hot-key to wrap.
            /// </param>
            /// <param name="advanced">
            /// Non-zero to use the advanced display form.
            /// </param>
            public HotKeyListItem(
                IHotKey hotKey, /* in */
                bool advanced   /* in */
                )
            {
                this.hotKey = hotKey;
                this.advanced = advanced;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Methods
            /// <summary>
            /// Masks the hot-key flags down to those shown by the viewer.
            /// </summary>
            /// <returns>
            /// The masked flags.
            /// </returns>
            private HotKeyFlags MaskFlags()
            {
                if ((hotKey == null) || hotKey.Disposed)
                    return HotKeyFlags.None;

                return hotKey.Flags & HotKeyFlags.ViewMask;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Properties
            /// <summary>
            /// Gets the hot-key represented by this list item.
            /// </summary>
            public IHotKey HotKey
            {
                get { return hotKey; }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region System.Object Overrides
            /// <summary>
            /// Returns the display text for this list item.
            /// </summary>
            /// <returns>
            /// The display text for the hot-key.
            /// </returns>
            public override string ToString()
            {
                //
                // BUGFIX: If the hot-key is disposed, it cannot be used to
                //         fetch the display tag.  How did this work before
                //         this change?  Perhaps the .NET Framework used to
                //         avoid refreshing its list control items when its
                //         items collection was cleared?
                //
                if ((hotKey == null) || hotKey.Disposed)
                    return null;

                //
                // NOTE: Attempt to obtain the display name from the hot-key
                //       -OR- null if it is not present.
                //
                string displayName = hotKey.GetDisplayTag(null);

                //
                // NOTE: This formatted string is what ends up being displayed
                //       to the user in the list box on the viewer form.
                //
                if (advanced)
                {
                    //
                    // NOTE: Advanced mode, show all the properties, including
                    //       the display name, if any.
                    //
                    return String.Format("{0}{1}",
                        (displayName != null) ? String.Format("name {0} ",
                        Parser.Quote(displayName)) : String.Empty, hotKey);
                }
                else
                {
                    //
                    // NOTE: Normal mode, show the summary only, including the
                    //       display name, if any.
                    //
                    return String.Format(
                        "{0}Id: #{1}, Keys: [{2}], Flags: [{3}]",
                        (displayName != null) ? String.Format("Name: {0}, ",
                        Utility.FormatWrapOrNull(displayName)) : String.Empty,
                        hotKey.Id, WinFormsOps.GetKeysToShow(hotKey.Keys),
                        MaskFlags());
                }
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
        /// <summary>
        /// The sentinel id representing no hot-key.
        /// </summary>
        private const string NullId = "null";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The text of the register/unregister button in its register state.
        /// </summary>
        private const string ButtonRegisterText = "Re&gister";
        /// <summary>
        /// The text of the register/unregister button in its unregister state.
        /// </summary>
        private const string ButtonUnregisterText = "Unre&gister";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The text of the edit button.
        /// </summary>
        private const string ButtonEditText = "&Edit";
        /// <summary>
        /// The text of the edit button in view-only state.
        /// </summary>
        private const string ButtonViewText = "V&iew";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prompt text shown when closing with unsaved changes.
        /// </summary>
        private static readonly string ClosingQuestionText =
            "It appears there are unsaved changes to one or more hot-keys." +
            Environment.NewLine + Environment.NewLine +
            "Save all changes before closing?";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The title of the open-file dialog.
        /// </summary>
        private const string OpenFileDialogTitle = "Open Hot-Key File";
        /// <summary>
        /// The title of the save-file dialog.
        /// </summary>
        private const string SaveFileDialogTitle = "Save Hot-Key File";

        /// <summary>
        /// The file filter used for Eagle script files.
        /// </summary>
        private const string EagleScriptFilter =
            "Eagle Files (*.eagle)|*.eagle|All Files (*.*)|*.*";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The maximum number of milliseconds to wait for the interactive-loop
        /// thread to die.
        /// </summary>
        private static readonly int ThreadJoinTimeout = 3000;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The script evaluated to show the about information.
        /// </summary>
        private static readonly string AboutScript =
            ScriptOps.commandName + " about";

        /// <summary>
        /// The script evaluated to auto-load hot-key files.
        /// </summary>
        private static readonly string AutoLoadScript =
            ScriptOps.commandName + " autoload";

        /// <summary>
        /// The script evaluated to start the interactive read-eval-print loop.
        /// </summary>
        private static readonly string ReplScript =
            "package require HotKey.Template.Common; showConsole";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        #region Associated Eagle Interpreter
        /// <summary>
        /// The interpreter associated with the viewer.
        /// </summary>
        private Interpreter interpreter;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Interactive Loop Thread
        /// <summary>
        /// The thread running the interactive read-eval-print loop, if any.
        /// </summary>
        private Thread interactiveLoopThread;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Advanced Mode Flag
        /// <summary>
        /// Non-zero when the viewer is in advanced mode.
        /// </summary>
        private bool advanced;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Dirty (i.e. Unsaved Data) Flag
        /// <summary>
        /// Non-zero when the viewer has unsaved changes.
        /// </summary>
        private bool dirty;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Last Load/Save File Name
        /// <summary>
        /// The most recently used file name for save/load.
        /// </summary>
        private string lastFileName;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Pending Load Flag For Hot-Key List Items
        /// <summary>
        /// Non-zero while list items are being (re)loaded, to suppress
        /// item-check handling.
        /// </summary>
        private bool itemLoadActive;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Previous Selected Hot-Key Index
        /// <summary>
        /// The previously selected list index.
        /// </summary>
        private int previousSelectedIndex;
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="HotKeyViewForm" /> with the specified
        /// id, interpreter, result variable name, and mode.
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
        /// <param name="advanced">
        /// Non-zero to start in advanced mode.
        /// </param>
        private HotKeyViewForm(
            int id,                  /* in */
            Interpreter interpreter, /* in */
            string varName,          /* in */
            bool advanced            /* in */
            )
            : base(id, interpreter, varName)
        {
            InitializeComponent();

            ///////////////////////////////////////////////////////////////////

            this.interpreter = interpreter;
            this.interactiveLoopThread = null; /* TODO: Pass me? */

            ///////////////////////////////////////////////////////////////////

            SetAdvanced(advanced);
            SetDirty(false);
            SetLastFileName(null);

            ///////////////////////////////////////////////////////////////////

            itemLoadActive = false;
            previousSelectedIndex = Index.Invalid;

            ///////////////////////////////////////////////////////////////////

            this.FormClosing += new FormClosingEventHandler(
                HotKeyViewForm_FormClosing);

            this.Disposed += new EventHandler(HotKeyViewForm_Disposed);

            lstKey.SelectedIndexChanged += new EventHandler(
                lstKey_SelectedIndexChanged);

            lstKey.ItemCheck += new ItemCheckEventHandler(lstKey_ItemCheck);
            btnClear.Click += new EventHandler(btnClear_Click);
            btnAdd.Click += new EventHandler(btnAdd_Click);
            btnEdit.Click += new EventHandler(btnEdit_Click);
            btnRemove.Click += new EventHandler(btnRemove_Click);
            btnEvaluate.Click += new EventHandler(btnEvaluate_Click);
            btnRegister.Click += new EventHandler(btnRegister_Click);
            btnLoad.Click += new EventHandler(btnLoad_Click);
            btnSave.Click += new EventHandler(btnSave_Click);
            btnAutoLoad.Click += new EventHandler(btnAutoLoad_Click);
            btnREPL.Click += new EventHandler(btnREPL_Click);
            btnCopyLog.Click += new EventHandler(btnCopyLog_Click);
            btnAbout.Click += new EventHandler(btnAbout_Click);

            ///////////////////////////////////////////////////////////////////

            SetupButtonsForHotKey(null);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Event Handlers
        /// <summary>
        /// Handles the form-closing event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyViewForm_FormClosing(
            object sender,         /* in */
            FormClosingEventArgs e /* in */
            )
        {
            if (IsDirty())
            {
                //
                // NOTE: The dirty flag is set for this form, prompt the user
                //       to save changes now.
                //
                switch (WinFormsOps.YesNoOrCancel(this, ClosingQuestionText))
                {
                    case DialogResult.Yes:
                        {
                            //
                            // TODO: Is it a good idea to block saving of
                            //       zero hot-keys?  The theory here is
                            //       that if there are no hot-keys, you
                            //       have nothing to lose.
                            //
                            if (CountOfHotKeys() > 0)
                            {
                                //
                                // NOTE: For the sake of simplicity, just
                                //       call the save button from here.
                                //
                                /* NO RESULT */
                                btnSave_Click(sender, e);

                                //
                                // NOTE: Next, attempt to verify that the
                                //       dirty flag is no longer set; if it
                                //       still is, cancel the form closing.
                                //       There should be no need to display
                                //       any kind of error message here as
                                //       that should have been done by the
                                //       save button itself, if necessary.
                                //
                                if (IsDirty())
                                    goto default;
                            }
                            else
                            {
                                //
                                // NOTE: Issue an error message.
                                //
                                WinFormsOps.ShowError(this,
                                    "There are no hot-keys to save.");

                                goto default;
                            }
                            break;
                        }
                    case DialogResult.No:
                        {
                            //
                            // NOTE: Just in case we fail to actually close
                            //       the form, prevent further prompts to
                            //       save by resetting the dirty flag now.
                            //
                            SetDirty(false);
                            break;
                        }
                    case DialogResult.Cancel: /* FALL-THROUGH */
                    default:
                        {
                            //
                            // NOTE: Otherwise, cancel closing the form.
                            //
                            e.Cancel = true; /* TODO: Safe default? */
                            break;
                        }
                }
            }
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
        private void HotKeyViewForm_Disposed(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            if (!disposed)
            {
                //
                // NOTE: Attempt to gracefully shutdown the interactive loop
                //       thread.  If this fails, we will automatically resort
                //       to harsher methods.
                //
                StopInteractiveLoopThread(true);

                //
                // NOTE: Forcibly shutdown and/or terminate the interactive
                //       loop thread using whatever means are necessary.
                //
                DisposeInteractiveLoopThread();

                //
                // NOTE: This form is now disposed.
                //
                disposed = true;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the list selected-index-changed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void lstKey_SelectedIndexChanged(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKey hotKey = null;

            /* IGNORED */
            RequireHotKey(Index.Invalid, ref hotKey);

            /* NO RESULT */
            SetupButtonsForHotKey(hotKey);

            //
            // NOTE: The currently selected list index is now the previously
            //       selected list index as well.  This will allow the item
            //       check event to work for the selected hot-key.
            //
            previousSelectedIndex = lstKey.SelectedIndex;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the list item-check event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void lstKey_ItemCheck(
            object sender,       /* in */
            ItemCheckEventArgs e /* in */
            )
        {
            //
            // NOTE: We must have the event data; therefore, if it was not
            //       provided, stop processing now.
            //
            if (e == null)
                return;

            //
            // HACK: We *NEVER* want to allow a hot-key to be registered via
            //       this event when it was not already the selected hot-key.
            //
            if (e.Index != previousSelectedIndex)
            {
                if (!itemLoadActive)
                    e.NewValue = e.CurrentValue;

                return;
            }

            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(false, ref hotKeyManager))
                return;

            IHotKey hotKey = null;

            if (!RequireHotKey(e.Index, ref hotKey))
                return;

            ReturnCode code;
            Result error = null;

            switch (e.NewValue)
            {
                case CheckState.Unchecked:
                    {
                        if (!hotKey.Registered)
                            return;

                        bool saveNotify = hotKeyManager.Notify;
                        hotKeyManager.Notify = false;

                        try
                        {
                            code = hotKey.Unregister(ref error);
                        }
                        finally
                        {
                            hotKeyManager.Notify = saveNotify;
                        }
                        break;
                    }
                case CheckState.Checked:
                    {
                        if (hotKey.Registered)
                            return;

                        bool saveNotify = hotKeyManager.Notify;
                        hotKeyManager.Notify = false;

                        try
                        {
                            code = hotKey.Register(ref error);
                        }
                        finally
                        {
                            hotKeyManager.Notify = saveNotify;
                        }
                        break;
                    }
                default:
                    {
                        code = ReturnCode.Ok;
                        break;
                    }
            }

            if (code == ReturnCode.Ok)
            {
                //
                // NOTE: Success, mark this form as dirty because the state of
                //       the list of hot-keys has changed (i.e. at least one of
                //       the "WasRegistered" flags has been changed).
                //
                SetDirty(true);
            }
            else
            {
                //
                // NOTE: We failed to register or unregister the hot-key;
                //       therefore, make sure that the checked state for this
                //       hot-key remains unchanged.
                //
                e.NewValue = e.CurrentValue;

                //
                // NOTE: Complain to the user about being unable to register
                //       or unregister the hot-key.
                //
                WinFormsOps.ShowResult(this, code, error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the clear-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnClear_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(false, ref hotKeyManager))
                return;

            int oldCount = CountOfHotKeys();

            ReturnCode code;
            Result error = null;

            code = hotKeyManager.ClearHotKeys(false, false, ref error);

            if (code == ReturnCode.Ok)
            {
                int newCount = CountOfHotKeys();

                //
                // NOTE: If the numer of hot-key is unchanged, that should mean
                //       the operation has failed and nothing has been changed.
                //
                if (newCount != oldCount)
                    SetDirty(true);
            }
            else
            {
                WinFormsOps.ShowResult(this, code, error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the add-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnAdd_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(false, ref hotKeyManager))
                return;

            ReturnCode code;
            IHotKey hotKey = null;
            Result error = null;

            code = HotKeyEditForm.ShowEditor(
                this, interpreter, null, FormId.GetNext(),
                false, false, true, ref hotKey, ref error);

            if ((code == ReturnCode.Ok) && (hotKey != null))
                code = hotKeyManager.AddHotKey(hotKey, ref error);

            if (code == ReturnCode.Ok)
            {
                if (hotKey != null)
                    SetDirty(true);
            }
            else
            {
                WinFormsOps.ShowResult(this, code, error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the remove-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnRemove_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(false, ref hotKeyManager))
                return;

            IHotKey hotKey = null;

            if (!RequireHotKey(Index.Invalid, ref hotKey))
                return;

            ReturnCode code;
            Result error = null;

            code = hotKeyManager.RemoveHotKey(hotKey.Id, ref error);

            if (code == ReturnCode.Ok)
                SetDirty(true);
            else
                WinFormsOps.ShowResult(this, code, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the register-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnRegister_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKey hotKey = null;

            if (!RequireHotKey(Index.Invalid, ref hotKey))
                return;

            ReturnCode code;
            Result error = null;

            if (hotKey.Registered)
                code = hotKey.Unregister(ref error);
            else
                code = hotKey.Register(ref error);

            if (code == ReturnCode.Ok)
                SetDirty(true);
            else
                WinFormsOps.ShowResult(this, code, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the save-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnSave_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(false, ref hotKeyManager))
                return;

            string fileName = WinFormsOps.SelectSaveFileName(
                SaveFileDialogTitle, EagleScriptFilter,
                ManagerOps.GetUserDirectory(), GetLastFileName());

            if (!String.IsNullOrEmpty(fileName))
            {
                ReturnCode code;
                string text = null;
                Result error = null;

                code = hotKeyManager.SaveHotKeys(false, ref text, ref error);

                if (code != ReturnCode.Ok)
                {
                    WinFormsOps.ShowResult(this, code, error);
                    return;
                }

                try
                {
                    File.WriteAllText(fileName, text); /* throw */

                    //
                    // NOTE: All content has been saved to disk, therefore,
                    //       this form is no longer dirty.
                    //
                    SetDirty(false);

                    //
                    // NOTE: Save the file name that was just used for later.
                    //
                    SetLastFileName(fileName);
                }
                catch (Exception ex)
                {
                    WinFormsOps.ShowResult(this, ReturnCode.Error, ex);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the load-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnLoad_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(false, ref hotKeyManager))
                return;

            string fileName = WinFormsOps.SelectOpenFileName(
                OpenFileDialogTitle, EagleScriptFilter,
                ManagerOps.GetUserDirectory(), GetLastFileName());

            if (!String.IsNullOrEmpty(fileName))
            {
                ReturnCode code;
                string text = null;
                Result error = null;

                code = ScriptOps.ReadFile(
                    interpreter, fileName, ref text, ref error);

                if (code != ReturnCode.Ok)
                {
                    WinFormsOps.ShowResult(this, code, error);
                    return;
                }

                code = hotKeyManager.LoadHotKeys(text, true, true, ref error);

                if (code == ReturnCode.Ok)
                    SetLastFileName(fileName);
                else
                    WinFormsOps.ShowResult(this, code, error);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the evaluate-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnEvaluate_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKey hotKey = null;

            if (!RequireHotKey(Index.Invalid, ref hotKey))
                return;

            /* NO RESULT */
            ScriptOps.ResetCancel(interpreter);

            /* NO RESULT */
            hotKey.EvaluateScript(
                interpreter, HotKeyScriptFlags.ViaUserInterface);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the edit-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnEdit_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(false, ref hotKeyManager))
                return;

            IHotKey hotKey = null;

            if (!RequireHotKey(Index.Invalid, ref hotKey))
                return;

            ReturnCode code;
            Result error = null;

            code = HotKeyEditForm.ShowEditor(
                this, interpreter, null, FormId.GetNext(),
                hotKey.Registered, false, true, ref hotKey,
                ref error);

            if ((code == ReturnCode.Ok) && !hotKey.Registered)
            {
                code = hotKeyManager.SetHotKey(
                    hotKey.Id, hotKey, ref error);
            }

            if (code == ReturnCode.Ok)
                SetDirty(true);
            else
                WinFormsOps.ShowResult(this, code, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the auto-load-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnAutoLoad_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            ReturnCode code;
            Result result = null;

            code = ScriptOps.Evaluate(
                interpreter, AutoLoadScript, false, true, true, false,
                ref result);

            if (code != ReturnCode.Ok)
                WinFormsOps.ShowResult(this, code, result);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the REPL-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnREPL_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            ReturnCode code;
            Result result = null;

            code = ScriptOps.Evaluate(
                interpreter, ReplScript, false, false, true, false,
                ref result);

            if (code == ReturnCode.Ok)
            {
                bool show = false;

                if ((Value.GetBoolean2(
                        result, ValueFlags.AnyBoolean,
                        (interpreter != null) ? interpreter.CultureInfo :
                        null, ref show, ref result) == ReturnCode.Ok))
                {
                    if (show)
                        /* NO RESULT */
                        StartInteractiveLoopThread();
                    else
                        /* NO RESULT */
                        StopInteractiveLoopThread(false);
                }
            }
            else
            {
                WinFormsOps.ShowResult(this, code, result);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the copy-log-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnCopyLog_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = Shell.Form.CopyLogToClipboard(ref error);

            if (code != ReturnCode.Ok)
                WinFormsOps.ShowResult(this, code, error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the about-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnAbout_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            ReturnCode code;
            Result result = null;

            code = ScriptOps.Evaluate(
                interpreter, AboutScript, false, true, true, false,
                ref result);

            WinFormsOps.ShowResult(this, code, result);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Determines whether the viewer is in advanced mode.
        /// </summary>
        /// <returns>
        /// Non-zero when in advanced mode; otherwise, zero.
        /// </returns>
        private bool IsAdvanced()
        {
            return advanced;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets whether the viewer is in advanced mode.
        /// </summary>
        /// <param name="advanced">
        /// Non-zero to enable advanced mode.
        /// </param>
        private void SetAdvanced(
            bool advanced /* in */
            )
        {
            this.advanced = advanced;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the viewer has unsaved changes.
        /// </summary>
        /// <returns>
        /// Non-zero when the viewer is dirty; otherwise, zero.
        /// </returns>
        private bool IsDirty()
        {
            return dirty;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets whether the viewer has unsaved changes.
        /// </summary>
        /// <param name="dirty">
        /// Non-zero to mark the viewer dirty.
        /// </param>
        private void SetDirty(
            bool dirty /* in */
            )
        {
            this.dirty = dirty;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the most recently used file name.
        /// </summary>
        /// <returns>
        /// The last file name.
        /// </returns>
        private string GetLastFileName()
        {
            return lastFileName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the most recently used file name.
        /// </summary>
        /// <param name="fileName">
        /// The file name to record.
        /// </param>
        private void SetLastFileName(
            string fileName /* in */
            )
        {
            lastFileName = fileName;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a displayable identifier for the supplied thread.
        /// </summary>
        /// <param name="thread">
        /// The thread to identify.
        /// </param>
        /// <returns>
        /// The thread identifier.
        /// </returns>
        private string GetThreadId(
            Thread thread /* in */
            )
        {
            if (thread == null)
                return NullId;

            return thread.ManagedThreadId.ToString();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Starts the interactive read-eval-print loop thread.
        /// </summary>
        private void StartInteractiveLoopThread()
        {
            if ((interactiveLoopThread != null) &&
                interactiveLoopThread.IsAlive)
            {
                LogOps.LogOrComplain(interpreter, String.Format(
                    "interactive loop thread {0} is still alive",
                    GetThreadId(interactiveLoopThread)));

                return;
            }

            Result error = null;

            interactiveLoopThread = Utility.CreateInteractiveLoopThread(
                interpreter, null, true, ref error);

            if (interactiveLoopThread != null)
            {
                LogOps.LogOrComplain(interpreter, String.Format(
                    "started interactive loop thread {0}",
                    GetThreadId(interactiveLoopThread)));
            }
            else
            {
                LogOps.LogOrComplain(interpreter, String.Format(
                    "failed to start interactive loop thread: {0}",
                    Utility.FormatWrapOrNull(true, false, error)));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Stops the interactive read-eval-print loop thread.
        /// </summary>
        /// <param name="quiet">
        /// Non-zero to suppress complaints on failure.
        /// </param>
        private void StopInteractiveLoopThread(
            bool quiet /* in */
            )
        {
            ReturnCode code;
            Result error = null;

            code = Utility.StopInteractiveLoopThread(
                interactiveLoopThread, interpreter, false, ref error);

            if (code == ReturnCode.Ok)
            {
                if (!quiet)
                    LogOps.LogOrComplain(interpreter, String.Format(
                        "stopped interactive loop thread {0}",
                        GetThreadId(interactiveLoopThread)));

                interactiveLoopThread = null;
            }
            else if (!quiet)
            {
                LogOps.LogOrComplain(interpreter, String.Format(
                    "failed to stop interactive loop thread: {0}",
                    Utility.FormatWrapOrNull(true, false, error)));
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Disposes the interactive read-eval-print loop thread.
        /// </summary>
        private void DisposeInteractiveLoopThread()
        {
            try
            {
                if (interactiveLoopThread != null)
                {
                    if (interactiveLoopThread.IsAlive)
                    {
                        interactiveLoopThread.Interrupt();

                        if (!interactiveLoopThread.Join(
                                ThreadJoinTimeout) &&
                            interactiveLoopThread.IsAlive &&
                            ((interpreter == null) ||
                            !interpreter.NoThreadAbort))
                        {
                            interactiveLoopThread.Abort();
                        }
                    }

                    interactiveLoopThread = null;
                }
            }
            catch (Exception ex)
            {
                LogOps.Complain(interpreter, ReturnCode.Error, ex);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the number of hot-keys shown in the viewer.
        /// </summary>
        /// <returns>
        /// The hot-key count.
        /// </returns>
        private int CountOfHotKeys()
        {
            return lstKey.Items.Count;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates the viewer buttons to reflect the state of the supplied
        /// hot-key.
        /// </summary>
        /// <param name="hotKey">
        /// The selected hot-key, or null when none.
        /// </param>
        private void SetupButtonsForHotKey(
            IHotKey hotKey /* in */
            )
        {
            int count = CountRegistered();

            WinFormsOps.Invoke(this, new DelegateWithNoArgs(delegate()
            {
                btnClear.Enabled = (count == 0);
                btnAdd.Enabled = true;

                if (hotKey != null)
                {
                    btnEdit.Enabled = true;

                    btnEdit.Text = hotKey.Registered ?
                        ButtonViewText : ButtonEditText;

                    btnRemove.Enabled = !hotKey.Registered;
                    btnEvaluate.Enabled = true;
                    btnRegister.Enabled = true;

                    btnRegister.Text = hotKey.Registered ?
                        ButtonUnregisterText : ButtonRegisterText;
                }
                else
                {
                    btnEdit.Enabled = false;
                    btnEdit.Text = ButtonEditText;
                    btnRemove.Enabled = false;
                    btnEvaluate.Enabled = false;
                    btnRegister.Enabled = false;
                    btnRegister.Text = ButtonRegisterText;
                }

                btnLoad.Enabled = true;

                //
                // TODO: Is it a good idea to block saving of zero hot-keys?
                //       The theory here is that if there are no hot-keys,
                //       you have nothing to lose.
                //
                btnSave.Enabled = (CountOfHotKeys() > 0);
            }), true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the hot-key manager, reporting an error when it is
        /// unavailable.
        /// </summary>
        /// <param name="quiet">
        /// Non-zero to suppress complaints on failure.
        /// </param>
        /// <param name="hotKeyManager">
        /// On output, receives the hot-key manager.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool RequireHotKeyManager(
            bool quiet,                      /* in */
            ref IHotKeyManager hotKeyManager /* out */
            )
        {
            hotKeyManager = Shell.Form.GetHotKeyManager();

            if (hotKeyManager == null)
            {
                if (!quiet)
                    WinFormsOps.ShowError(this, "Invalid hot-key manager");

                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the hot-key at the specified list index, reporting an error
        /// when it is unavailable.
        /// </summary>
        /// <param name="index">
        /// The list index.
        /// </param>
        /// <param name="hotKey">
        /// On output, receives the hot-key.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private bool RequireHotKey(
            int index,         /* in */
            ref IHotKey hotKey /* out */
            )
        {
            HotKeyListItem item;

            if ((index >= 0) && (index < lstKey.Items.Count))
                item = lstKey.Items[index] as HotKeyListItem;
            else
                item = lstKey.SelectedItem as HotKeyListItem;

            if (item == null)
            {
                WinFormsOps.ShowError(this, "No hot-key item is selected");
                return false;
            }

            hotKey = item.HotKey;

            if (hotKey == null)
            {
                WinFormsOps.ShowError(this, "Invalid hot-key selected");
                return false;
            }

            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads (or reloads) the hot-keys from the manager into the viewer
        /// list.
        /// </summary>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private ReturnCode LoadViewer(
            ref Result error /* out */
            )
        {
            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(true, ref hotKeyManager))
            {
                error = "invalid hot-key manager";
                return ReturnCode.Error;
            }

            ReturnCode code;
            IntList ids = null;

            code = hotKeyManager.ListHotKeys(ref ids, ref error);

            if (code != ReturnCode.Ok)
                return code;

            SetupButtonsForHotKey(null);
            previousSelectedIndex = Index.Invalid;

            WinFormsOps.Invoke(lstKey, new DelegateWithNoArgs(delegate()
            {
                lstKey.Items.Clear();
            }), true);

            itemLoadActive = true;

            try
            {
                foreach (int id in ids)
                {
                    IHotKey hotKey = null;

                    code = hotKeyManager.GetHotKey(id, ref hotKey, ref error);

                    if (code != ReturnCode.Ok)
                        return code;

                    WinFormsOps.Invoke(lstKey, new DelegateWithNoArgs(delegate()
                    {
                        //
                        // NOTE: For some reason, this actually will raise the
                        //       ItemCheck event for the checked list box.
                        //
                        lstKey.Items.Add(
                            new HotKeyListItem(hotKey, IsAdvanced()),
                            hotKey.Registered);
                    }), true);
                }
            }
            finally
            {
                itemLoadActive = false;
            }

            SetupButtonsForHotKey(null);
            return ReturnCode.Ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the number of currently registered hot-keys.
        /// </summary>
        /// <returns>
        /// The registered hot-key count.
        /// </returns>
        private int CountRegistered()
        {
            IHotKeyManager hotKeyManager = null;

            if (!RequireHotKeyManager(true, ref hotKeyManager))
                return 0; /* UNKNOWN */

            ReturnCode code;
            Result error = null;
            int count = 0;

            code = hotKeyManager.CountHotKeys(true, ref count, ref error);

            if (code != ReturnCode.Ok)
            {
                LogOps.Complain(interpreter, code, error);
                return 0; /* UNKNOWN */
            }

            return count;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHotKeyViewer Members
        /// <summary>
        /// Refreshes the viewer's displayed hot-key information.
        /// </summary>
        /// <param name="interactive">
        /// Non-zero when the refresh was initiated interactively.
        /// </param>
        public void Refresh(
            bool interactive /* in */
            )
        {
            CheckDisposed();

            ReturnCode code;
            Result error = null;

            code = LoadViewer(ref error);

            if (code != ReturnCode.Ok)
            {
                if (interactive)
                    WinFormsOps.ShowResult(this, code, error);
                else
                    LogOps.Complain(interpreter, code, error);
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Creates and shows the hot-key viewer form.
        /// </summary>
        /// <param name="owner">
        /// The owner window, if any.
        /// </param>
        /// <param name="interpreter">
        /// The associated interpreter.
        /// </param>
        /// <param name="varName">
        /// The variable that receives the form id, if any.
        /// </param>
        /// <param name="id">
        /// The form id.
        /// </param>
        /// <param name="advanced">
        /// Non-zero to start in advanced mode.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowViewer(
            IWin32Window owner,           /* in */
            Interpreter interpreter,      /* in */
            string varName,               /* in */
            int id,                       /* in */
            bool advanced,                /* in */
            ref Result error              /* out */
            )
        {
            try
            {
                using (HotKeyViewForm form = new HotKeyViewForm(
                        id, interpreter, varName, advanced))
                {
                    if (form.LoadViewer(ref error) == ReturnCode.Ok)
                    {
                        if (form.ShowDialog(owner) == DialogResult.OK)
                            return ReturnCode.Ok;
                        else
                            error = "hot-key viewer is now closed";
                    }
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
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
                throw new ObjectDisposedException(typeof(HotKeyViewForm).Name);
#endif
        }
        #endregion
    }
}
