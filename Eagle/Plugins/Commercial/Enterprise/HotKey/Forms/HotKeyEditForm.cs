/*
 * HotKeyEditForm.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Drawing;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Interfaces.Public;
using HotKey.Components.Private;
using HotKey.Interfaces.Private;

namespace HotKey.Forms
{
    /// <summary>
    /// Implements the hot-key editor form, which edits a hot-key's keys,
    /// flags, descriptive text, and script.  It supports basic and advanced
    /// modes, template insertion, key selection, script evaluation, and
    /// applying a script result back to the text.
    /// </summary>
    [ObjectId("476a257b-0566-4160-b8d7-20d858e9e561")]
    internal sealed partial class HotKeyEditForm :
            BaseEditForm, IHotKeyEditorResult
    {
        #region Private Constants
        /// <summary>
        /// The text of the mode button in its basic-mode state.
        /// </summary>
        private const string ButtonBasicText = "&Basic";
        /// <summary>
        /// The text of the mode button in its advanced-mode state.
        /// </summary>
        private const string ButtonAdvancedText = "A&dvanced";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prompt text shown before evaluating the script.
        /// </summary>
        private static readonly string EvaluateQuestionText =
            "This will create a temporary hot-key and evaluate its current " +
            "script text using the selected flags." + Environment.NewLine +
            Environment.NewLine + "Are you sure?";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prompt text shown when closing with unsaved changes.
        /// </summary>
        private static readonly string ClosingQuestionText =
            "It appears there are unsaved changes to this hot-key." +
            Environment.NewLine + Environment.NewLine +
            "Save changes before closing?";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        #region Associated Eagle Interpreter
        /// <summary>
        /// The interpreter associated with the editor.
        /// </summary>
        private Interpreter interpreter;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Control Sizing Values
        /// <summary>
        /// The minimum width of the form.
        /// </summary>
        private int MinimumWidth;
        /// <summary>
        /// The margin between buttons.
        /// </summary>
        private int ButtonMargin;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Basic Mode Control Top/Height Values
        /// <summary>
        /// The form height in basic mode.
        /// </summary>
        private int BasicFormHeight;
        /// <summary>
        /// The top position of the template controls in basic mode.
        /// </summary>
        private int BasicTemplateTop;
        /// <summary>
        /// The top position of the text editor in basic mode.
        /// </summary>
        private int BasicTextTop;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Advanced Mode Control Top/Height Values
        /// <summary>
        /// The form height in advanced mode.
        /// </summary>
        private int AdvancedFormHeight;
        /// <summary>
        /// The top position of the template controls in advanced mode.
        /// </summary>
        private int AdvancedTemplateTop;
        /// <summary>
        /// The top position of the text editor in advanced mode.
        /// </summary>
        private int AdvancedTextTop;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Scintilla Support (Line Number Margin / Text Zoom)
#if SCINTILLA
        /// <summary>
        /// The previous zoom level, used to detect zoom changes.
        /// </summary>
        private int previousZoomLevel;
        /// <summary>
        /// The previous line count, used to resize the line-number margin.
        /// </summary>
        private int previousLineCount;
        /// <summary>
        /// The font used at the current zoom level.
        /// </summary>
        private Font zoomFont;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Basic/Advanced Mode Flag
        /// <summary>
        /// Non-zero when the editor is in advanced mode.
        /// </summary>
        private bool advanced;
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="HotKeyEditForm" /> with the specified
        /// id, interpreter, and result variable name.
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
        private HotKeyEditForm(
            int id,                  /* in */
            Interpreter interpreter, /* in */
            string varName           /* in */
            )
            : base(id, interpreter, varName)
        {
            InitializeComponent();

            ///////////////////////////////////////////////////////////////////

            this.interpreter = interpreter;

            ///////////////////////////////////////////////////////////////////

            #region Scintilla Support (Language Configuration)
#if SCINTILLA
            bool isolated = Shell.Form.IsHotKeyIsolated(interpreter);

            ///////////////////////////////////////////////////////////////////

            ScintillaOps.PreConfigure(interpreter, txtText, isolated, true);
            ScintillaOps.Configure(interpreter, txtText, isolated, true);
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            InitializeModes();
            SetDirty(false);

            ///////////////////////////////////////////////////////////////////

            this.FormClosed += new FormClosedEventHandler(
                HotKeyEditForm_FormClosed);

            this.Disposed += new EventHandler(HotKeyEditForm_Disposed);

            lstFlag.ItemCheck += new ItemCheckEventHandler(lstFlag_ItemCheck);
            txtText.TextChanged += new EventHandler(txtText_TextChanged);

            ///////////////////////////////////////////////////////////////////

            #region Scintilla Support (Event Setup)
#if SCINTILLA
#if SCINTILLA_30
            txtText.ZoomChanged += new EventHandler<EventArgs>(
                txtText_ZoomFactorChanged);
#else
            txtText.ZoomFactorChanged += new EventHandler(
                txtText_ZoomFactorChanged);
#endif
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            btnTemplate.Click += new EventHandler(btnTemplate_Click);
            btnSelectKeys.Click += new EventHandler(btnSelectKeys_Click);
            btnMode.Click += new EventHandler(btnMode_Click);
            btnEvaluate.Click += new EventHandler(btnEvaluate_Click);
            btnSave.Click += new EventHandler(btnSave_Click);
            btnCancel.Click += new EventHandler(btnCancel_Click);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Event Handlers
        #region Form Event Handlers
        /// <summary>
        /// Handles the form-closed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void HotKeyEditForm_FormClosed(
            object sender,        /* in */
            FormClosedEventArgs e /* in */
            )
        {
            //
            // NOTE: If we have an interpreter, cancel any running hot-key
            //       template scripts now.
            //
            if (interpreter != null)
            {
                ScriptOps.EnterPendingCancel();

                try
                {
                    ReturnCode cancelCode;
                    Result cancelResult = null;

                    cancelCode = TemplateOps.Cancel(
                        interpreter, ref cancelResult);

                    if (cancelCode != ReturnCode.Ok)
                    {
                        LogOps.Complain(
                            interpreter, cancelCode, cancelResult);
                    }
                }
                finally
                {
                    ScriptOps.ExitPendingCancel();
                }
            }

            //
            // NOTE: Next, close all the associated (and open) busy forms.
            //
            BaseForm.CloseOneOrAll(typeof(BusyForm), 0, false);
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
        private void HotKeyEditForm_Disposed(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            if (!disposed)
            {
#if SCINTILLA
                if (zoomFont != null)
                {
                    zoomFont.Dispose();
                    zoomFont = null;
                }
#endif

                //
                // NOTE: This form is now disposed.
                //
                disposed = true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Data Event Handlers
        /// <summary>
        /// Handles the flag list item-check event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void lstFlag_ItemCheck(
            object sender,       /* in */
            ItemCheckEventArgs e /* in */
            )
        {
            if (e == null)
                return;

            HotKeyFlags flags = SaveFlags(null, HotKeyFlags.None);
            object item = lstFlag.Items[e.Index];

            if (item is HotKeyFlags)
            {
                HotKeyFlags itemFlags = (HotKeyFlags)item;

                if (e.NewValue == CheckState.Checked)
                    flags |= itemFlags;
                else
                    flags &= ~itemFlags;
            }

            SetModeBackColor(flags);
            SetDirty(true);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the text text-changed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void txtText_TextChanged(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
#if SCINTILLA
            int lineCount = txtText.Lines.Count;

            if (lineCount != previousLineCount)
            {
                ScintillaOps.SetMargin0Width(txtText, lineCount,
                    (zoomFont != null) ? zoomFont : txtText.Font);

                previousLineCount = lineCount;
            }
#else
            SetDirty(true);
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Scintilla Support (Event Handlers)
#if SCINTILLA
        /// <summary>
        /// Handles the text zoom-factor-changed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void txtText_ZoomFactorChanged(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
#if SCINTILLA_30
            int zoomLevel = txtText.Zoom;
#else
            int zoomLevel = txtText.ZoomFactor;
#endif

            if (zoomLevel != previousZoomLevel)
            {
                Font font = txtText.Font;
                float zoomSize = font.Size + zoomLevel;

                if (zoomFont != null)
                    zoomFont.Dispose();

                zoomFont = ScintillaOps.MakeFont(font, zoomSize);

                ScintillaOps.SetMargin0Width(
                    txtText, txtText.Lines.Count, zoomFont);

                previousZoomLevel = zoomLevel;
            }
        }
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Button Event Handlers
        /// <summary>
        /// Handles the template-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnTemplate_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            if (btnTemplate.Enabled)
            {
                int index = cboTemplate.SelectedIndex;

                if ((index >= 0) && (index < cboTemplate.Items.Count))
                {
                    HotKeyTemplateType templateType =
                        (HotKeyTemplateType)cboTemplate.Items[index];

                    if (templateType != HotKeyTemplateType.Invalid)
                    {
                        bool asynchronous = CanUseAsynchronous();
                        bool append = true;

                        ReturnCode code;
                        Result result = null;

                        code = TemplateOps.Evaluate(
                            interpreter, this, templateType, true, true,
                            asynchronous, true, append, true, ref result);

                        if (asynchronous)
                        {
                            if (code != ReturnCode.Ok)
                                WinFormsOps.ShowResult(this, code, result);
                        }
                        else
                        {
                            try
                            {
                                ModifyTextFromResult(
                                    code, result, interpreter.ErrorLine,
                                    append);
                            }
                            catch (Exception ex)
                            {
                                LogOps.Complain(
                                    interpreter, ReturnCode.Error, ex);
                            }
                        }
                    }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the select-keys-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnSelectKeys_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            Keys modifiers = Keys.None;
            Keys virtualKey = Keys.None;

            if ((WinFormsOps.ParseModifiersAndVirtualKey(
                    null, txtKeys.Text, ref modifiers,
                    ref virtualKey) == ReturnCode.Ok) &&
                (SelectHotKeyForm.ShowKeyboard(
                    this, null, null, FormId.GetNext(),
                    ref modifiers, ref virtualKey) == ReturnCode.Ok))
            {
                txtKeys.Text = WinFormsOps.GetKeysToShow(
                    modifiers, virtualKey);

                SetDirty(true);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the mode-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnMode_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            try
            {
                SetMode(!advanced);
            }
            catch (Exception ex)
            {
                WinFormsOps.ShowResult(this, ReturnCode.Error, ex);
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
            if (WinFormsOps.YesOrNo(
                    this, EvaluateQuestionText) == DialogResult.Yes)
            {
                ReturnCode code;
                IHotKey hotKey = null;
                Result error = null;

                code = SaveEditor(interpreter, ref hotKey, ref error);

                if (code == ReturnCode.Ok)
                {
                    /* NO RESULT */
                    ScriptOps.ResetCancel(interpreter);

                    /* NO RESULT */
                    hotKey.EvaluateScript(
                        interpreter, HotKeyScriptFlags.ViaUserInterface);
                }
                else
                {
                    WinFormsOps.ShowResult(this, code, error);
                }
            }
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
            SetSave(true);
            this.Hide();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the cancel-button click event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void btnCancel_Click(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            SetSave(false);
            this.Hide();
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        #region Mode Support
        /// <summary>
        /// Gets the height offset applied in basic mode.
        /// </summary>
        /// <returns>
        /// The height offset.
        /// </returns>
        private int GetBasicHeightOffset()
        {
            //
            // NOTE: Figure out the proper offset to be used when converting
            //       advanced mode top/height values to basic mode top/height
            //       values.
            //
            return (grpKeys.Margin.Bottom + grpFlags.Margin.Top +
                grpFlags.Height);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the non-text height of the form in basic mode.
        /// </summary>
        /// <returns>
        /// The non-text height.
        /// </returns>
        private int GetBasicNonTextHeight()
        {
            //
            // NOTE: Return the full height of all the basic mode controls
            //       except the hot-key script text box.
            //
            return (grpKeys.Margin.Top + grpKeys.Height +
                grpKeys.Margin.Bottom + grpTemplate.Margin.Top +
                grpTemplate.Height + grpTemplate.Margin.Bottom +
                ButtonMargin + btnMode.Height + ButtonMargin);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the non-text height of the form in advanced mode.
        /// </summary>
        /// <returns>
        /// The non-text height.
        /// </returns>
        private int GetAdvancedNonTextHeight()
        {
            //
            // NOTE: Return the full height of all the advanced mode controls
            //       except the hot-key script text box.
            //
            return (grpKeys.Margin.Top + grpKeys.Height +
                grpKeys.Margin.Bottom + grpFlags.Margin.Top +
                grpFlags.Height + grpFlags.Margin.Bottom +
                grpTemplate.Margin.Top + grpTemplate.Height +
                grpTemplate.Margin.Bottom + ButtonMargin +
                btnMode.Height + ButtonMargin);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the top position used for the buttons.
        /// </summary>
        /// <returns>
        /// The button top position.
        /// </returns>
        private int GetTopForButtons()
        {
            //
            // NOTE: Return where the top of the buttons should be, based on
            //       the fact it should be right below the hot-key script text
            //       box, with a margin.
            //
            return (grpText.Top + grpText.Height + ButtonMargin);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes the basic and advanced editor modes.
        /// </summary>
        private void InitializeModes()
        {
            //
            // NOTE: Save the minimum width now as we will need it later on to
            //       build the new minimum size structures.
            //
            MinimumWidth = this.MinimumSize.Width;

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Figure out the "static" vertical margin between the text
            //       group box and the buttons.
            //
            ButtonMargin = pnlMode.Top - (grpText.Top + grpText.Height);

            ///////////////////////////////////////////////////////////////////

            //
            // HACK: This method basically assumes that the form was initially
            //       created in advanced mode (i.e. just as it appears in the
            //       designer view).
            //
            AdvancedFormHeight = this.Height;
            AdvancedTemplateTop = grpTemplate.Top;
            AdvancedTextTop = grpText.Top;

            ///////////////////////////////////////////////////////////////////

            int basicHeightOffset = -GetBasicHeightOffset();

            ///////////////////////////////////////////////////////////////////

            BasicFormHeight = AdvancedFormHeight + basicHeightOffset;
            BasicTemplateTop = AdvancedTemplateTop + basicHeightOffset;
            BasicTextTop = AdvancedTextTop + basicHeightOffset;

            ///////////////////////////////////////////////////////////////////

            this.advanced = true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Switches the editor between basic and advanced mode, resizing the
        /// form accordingly.
        /// </summary>
        /// <param name="advanced">
        /// Non-zero to switch to advanced mode.
        /// </param>
        private void SetMode(
            bool advanced /* in */
            )
        {
            this.SuspendLayout();

            if (advanced)
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    //
                    // BUGFIX: Only add the extra height difference between
                    //         basic and advanced mode if the form height is
                    //         less than it should be for advanced mode.
                    //
                    // BUGFIX: The above appears to be wrong.  Since the form
                    //         can be resized, it will not work right.
                    //
                    this.Height += GetBasicHeightOffset();
                }

                this.MinimumSize = new Size(MinimumWidth, AdvancedFormHeight);

                grpText.Top = AdvancedTextTop;

                if (this.WindowState == FormWindowState.Maximized)
                {
                    grpText.Height = this.ClientSize.Height -
                        (GetAdvancedNonTextHeight() + ButtonMargin);
                }

                pnlSave.Top = GetTopForButtons();
                pnlMode.Top = GetTopForButtons();
                btnMode.Text = ButtonBasicText;
                grpTemplate.Top = AdvancedTemplateTop;
                grpFlags.Visible = advanced;
            }
            else
            {
                grpFlags.Visible = advanced;
                grpTemplate.Top = BasicTemplateTop;

                grpText.Top = BasicTextTop;

                if (this.WindowState == FormWindowState.Maximized)
                {
                    grpText.Height = this.ClientSize.Height -
                        (GetBasicNonTextHeight() + ButtonMargin);
                }

                pnlSave.Top = GetTopForButtons();
                pnlMode.Top = GetTopForButtons();
                btnMode.Text = ButtonAdvancedText;

                this.MinimumSize = new Size(MinimumWidth, BasicFormHeight);

                if (this.WindowState == FormWindowState.Normal)
                    this.Height -= GetBasicHeightOffset();
            }

            this.ResumeLayout(false);
            this.advanced = advanced;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Dirty (i.e. Unsaved Data) Flag Support
        //
        // NOTE: This method abstracts checking the dirty flag for this form.
        //       When the ScintillaNET control is in use, its "Modified"
        //       property value will be consulted in addition to the dirty
        //       flag field.
        //
        /// <summary>
        /// Determines whether the editor has unsaved changes.
        /// </summary>
        /// <returns>
        /// Non-zero when the editor is dirty; otherwise, zero.
        /// </returns>
        protected override bool IsDirty()
        {
#if SCINTILLA
            return base.IsDirty() || txtText.Modified;
#else
            return base.IsDirty();
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Template Support
        //
        // NOTE: This method abstracts the bug present in ScintillaNET that
        //       prevents the hot-key template script result callback from
        //       actually modifying the text box (i.e. their control does
        //       not currently implement the Invoke/BeginInvoke patterns
        //       properly to allow cross-thread marshalling).  Please see:
        //
        //       https://stackoverflow.com/questions/14820169
        //
        //       Once the above bug is fixed in ScintillaNET, this method
        //       should no longer be necessary.
        //
        // UPDATE: This issue is fixed in the latest trunk code:
        //
        //         https://scintillanet.codeplex.com/workitem/33759
        //
        /// <summary>
        /// Determines whether the script may be evaluated asynchronously.
        /// </summary>
        /// <returns>
        /// Non-zero when asynchronous evaluation is allowed; otherwise, zero.
        /// </returns>
        private bool CanUseAsynchronous()
        {
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the available template types for the supplied hot-key into
        /// the editor.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key being edited.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero when the editor is read-only.
        /// </param>
        /// <param name="template">
        /// Non-zero when editing the hot-key's template.
        /// </param>
        private void LoadTemplateTypes(
            IHotKey hotKey, /* in */
            bool readOnly,  /* in */
            bool template   /* in */
            )
        {
            cboTemplate.Items.Clear();
            cboTemplate.Items.Add(HotKeyTemplateType.Unknown);
            cboTemplate.Items.Add(HotKeyTemplateType.Script);
            cboTemplate.Items.Add(HotKeyTemplateType.ScriptFile);
            cboTemplate.Items.Add(HotKeyTemplateType.Metadata);
            cboTemplate.Items.Add(HotKeyTemplateType.Executable);
            cboTemplate.Items.Add(HotKeyTemplateType.Application);
            cboTemplate.Items.Add(HotKeyTemplateType.Program);
            cboTemplate.Items.Add(HotKeyTemplateType.File);
            cboTemplate.Items.Add(HotKeyTemplateType.Folder);
            cboTemplate.Items.Add(HotKeyTemplateType.URI);
            cboTemplate.Items.Add(HotKeyTemplateType.Bookmark);

            ///////////////////////////////////////////////////////////////////

            cboTemplate.Items.Add(HotKeyTemplateType.StandardMask);

            ///////////////////////////////////////////////////////////////////

            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined0);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined1);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined2);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined3);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined4);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined5);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined6);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined7);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined8);
            cboTemplate.Items.Add(HotKeyTemplateType.UserDefined9);

            ///////////////////////////////////////////////////////////////////

            cboTemplate.Enabled = !readOnly && template;
            btnTemplate.Enabled = !readOnly && template;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Selects the supplied template type in the editor.
        /// </summary>
        /// <param name="templateType">
        /// The template type to select.
        /// </param>
        private void SelectTemplateType(
            HotKeyTemplateType templateType /* in */
            )
        {
            cboTemplate.SelectedIndex = cboTemplate.FindString(
                templateType.ToString());
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hot-Key Flag Support
        /// <summary>
        /// Clears the flag list.
        /// </summary>
        private void ClearFlags()
        {
            lstFlag.Items.Clear();
            lstFlag.Items.Add(HotKeyFlags.NoComplain);
            lstFlag.Items.Add(HotKeyFlags.NoResetResult);
            lstFlag.Items.Add(HotKeyFlags.Asynchronous);
            lstFlag.Items.Add(HotKeyFlags.LogSynchronous);
            lstFlag.Items.Add(HotKeyFlags.LogAsynchronous);
            lstFlag.Items.Add(HotKeyFlags.WasRegistered);
            lstFlag.Items.Add(HotKeyFlags.FullyHandled);
            lstFlag.Items.Add(HotKeyFlags.NoLogHit);
            lstFlag.Items.Add(HotKeyFlags.NoLogError);
            lstFlag.Items.Add(HotKeyFlags.ResetCancel);
            lstFlag.Items.Add(HotKeyFlags.Isolated);
            lstFlag.Items.Add(HotKeyFlags.Safe);
            lstFlag.Items.Add(HotKeyFlags.NoRepeat);

            ///////////////////////////////////////////////////////////////////

#if WINFORMS
            lstFlag.Items.Add(HotKeyFlags.KeyEventManager);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the supplied hot-key's flags into the flag list.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key whose flags are loaded.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero when the editor is read-only.
        /// </param>
        private void LoadFlags(
            IHotKey hotKey, /* in */
            bool readOnly   /* in */
            )
        {
            if (hotKey == null)
                return;

            lstFlag.Items.Clear();

            ///////////////////////////////////////////////////////////////////

            lstFlag.Items.Add(HotKeyFlags.NoComplain, hotKey.HasFlags(
                HotKeyFlags.NoComplain, true));

            lstFlag.Items.Add(HotKeyFlags.NoResetResult, hotKey.HasFlags(
                HotKeyFlags.NoResetResult, true));

            lstFlag.Items.Add(HotKeyFlags.Asynchronous, hotKey.HasFlags(
                HotKeyFlags.Asynchronous, true));

            lstFlag.Items.Add(HotKeyFlags.LogSynchronous, hotKey.HasFlags(
                HotKeyFlags.LogSynchronous, true));

            lstFlag.Items.Add(HotKeyFlags.LogAsynchronous, hotKey.HasFlags(
                HotKeyFlags.LogAsynchronous, true));

            lstFlag.Items.Add(HotKeyFlags.WasRegistered, hotKey.HasFlags(
                HotKeyFlags.WasRegistered, true));

            lstFlag.Items.Add(HotKeyFlags.FullyHandled, hotKey.HasFlags(
                HotKeyFlags.FullyHandled, true));

            lstFlag.Items.Add(HotKeyFlags.NoLogHit, hotKey.HasFlags(
                HotKeyFlags.NoLogHit, true));

            lstFlag.Items.Add(HotKeyFlags.NoLogError, hotKey.HasFlags(
                HotKeyFlags.NoLogError, true));

            lstFlag.Items.Add(HotKeyFlags.ResetCancel, hotKey.HasFlags(
                HotKeyFlags.ResetCancel, true));

            lstFlag.Items.Add(HotKeyFlags.Isolated, hotKey.HasFlags(
                HotKeyFlags.Isolated, true));

            lstFlag.Items.Add(HotKeyFlags.Safe, hotKey.HasFlags(
                HotKeyFlags.Safe, true));

            lstFlag.Items.Add(HotKeyFlags.NoRepeat, hotKey.HasFlags(
                HotKeyFlags.NoRepeat, true));

            ///////////////////////////////////////////////////////////////////

#if WINFORMS
            lstFlag.Items.Add(HotKeyFlags.KeyEventManager, hotKey.HasFlags(
                HotKeyFlags.KeyEventManager, true));
#endif

            ///////////////////////////////////////////////////////////////////

            lstFlag.Enabled = !readOnly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the checked flags from the flag list into the supplied
        /// hot-key.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key that receives the flags.
        /// </param>
        /// <param name="defaultFlags">
        /// The default flags applied when none are checked.
        /// </param>
        private HotKeyFlags SaveFlags(
            IHotKey hotKey,          /* in */
            HotKeyFlags defaultFlags /* in */
            )
        {
            //
            // NOTE: *IMPORTANT* Make sure to preserve the flags that are not
            //       displayed in the editor user-interface, if any and then
            //       get the selected flags and add them to the default flags.
            //
            HotKeyFlags result = (hotKey != null) ?
                (hotKey.Flags & ~HotKeyFlags.ViewMask) : defaultFlags;

            for (int index = 0; index < lstFlag.Items.Count; index++)
            {
                if (!lstFlag.GetItemChecked(index))
                    continue;

                object enumValue = lstFlag.Items[index];

                if (!(enumValue is HotKeyFlags))
                    continue;

                result |= (HotKeyFlags)enumValue;
            }

            return result;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Editor Load/Save Support
        //
        // NOTE: This method abstracts the different properties required to set
        //       a text box or a ScintillaNET control into the read-only mode.
        //
        /// <summary>
        /// Sets whether the text editor is read-only.
        /// </summary>
        /// <param name="readOnly">
        /// Non-zero to make the editor read-only.
        /// </param>
        protected override void SetTextReadOnly(
            bool readOnly /* in */
            )
        {
#if SCINTILLA
#if SCINTILLA_30
            txtText.ReadOnly = readOnly;
#else
            txtText.IsReadOnly = readOnly;
#endif
#else
            txtText.ReadOnly = readOnly;
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the mode indicator's background color based on the supplied
        /// flags.
        /// </summary>
        /// <param name="flags">
        /// The flags used to choose the color.
        /// </param>
        private void SetModeBackColor(
            HotKeyFlags flags /* in */
            )
        {
            btnMode.ForeColor = (flags == HotKeyFlags.None) ?
                SystemColors.ControlText : Color.White;

            btnMode.BackColor = (flags == HotKeyFlags.None) ?
                SystemColors.Control : Color.Red;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the text editor.
        /// </summary>
        protected override void ClearEditor()
        {
            txtKeys.Text = null;
            btnSelectKeys.Enabled = true;
            lstFlag.Items.Clear();
            lstFlag.Text = null;
            lstFlag.Enabled = true;
            cboTemplate.Items.Clear();
            cboTemplate.Text = null;
            cboTemplate.Enabled = true;
            btnTemplate.Enabled = true;
            txtText.Text = null;
            SetTextReadOnly(false);
            btnSave.Enabled = true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the supplied text into the editor.
        /// </summary>
        /// <param name="text">
        /// The text to load.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero to load read-only.
        /// </param>
        protected override void LoadEditor(
            string text,  /* in */
            bool readOnly /* in */
            )
        {
            throw new NotImplementedException();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the supplied hot-key (keys, flags, text, and script) into the
        /// editor.
        /// </summary>
        /// <param name="hotKey">
        /// The hot-key to load.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero to load read-only.
        /// </param>
        private void LoadEditor(
            IHotKey hotKey, /* in */
            bool readOnly   /* in */
            )
        {
            if (hotKey == null)
                return;

            txtKeys.Text = WinFormsOps.GetKeysToShow(
                hotKey.Modifiers, hotKey.VirtualKey);

            btnSelectKeys.Enabled = !readOnly;

            LoadFlags(hotKey, readOnly);

            txtText.Text = ScriptOps.GetTextToLoad(hotKey.Text);
            SetTextReadOnly(readOnly);

            ///////////////////////////////////////////////////////////////////

            #region Scintilla Support (Text Box Properties)
#if SCINTILLA
#if SCINTILLA_30
            txtText.EmptyUndoBuffer();
#else
            txtText.UndoRedo.EmptyUndoBuffer();
            txtText.Modified = false;
#endif
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            SetModeBackColor(hotKey.Flags);

            ///////////////////////////////////////////////////////////////////

            btnSave.Enabled = !readOnly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the text editor content into the supplied reference argument.
        /// </summary>
        /// <param name="text">
        /// On output, receives the editor content.
        /// </param>
        protected override void SaveEditor(
            ref string text /* in, out */
            )
        {
            throw new NotImplementedException();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the editor content into the supplied hot-key, using the
        /// interpreter to parse and validate it.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter used to parse the content.
        /// </param>
        /// <param name="hotKey">
        /// The hot-key that receives the edited content.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        private ReturnCode SaveEditor(
            Interpreter interpreter, /* in */
            ref IHotKey hotKey,      /* in, out */
            ref Result error         /* out */
            )
        {
            IHotKeyManager hotKeyManager = Shell.Form.GetHotKeyManager();

            if (hotKeyManager == null)
            {
                error = "invalid hot-key manager";
                return ReturnCode.Error;
            }

            ReturnCode code;
            Keys keys = Keys.None;

            code = WinFormsOps.ParseKeys(
                interpreter, txtKeys.Text, ref keys, ref error);

            if (code != ReturnCode.Ok)
                return code;

            HotKeyFlags flags = SaveFlags(hotKey, HotKeyFlags.Default);
            string text = ScriptOps.GetTextToSave(txtText.Text, false);

            hotKey = HotKey.Components.Private.HotKey.Create(
                hotKeyManager, hotKey, keys, flags, text);

            return ReturnCode.Ok;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IHotKeyEditorResult Members
        /// <summary>
        /// The asynchronous callback invoked when a template script completes,
        /// applying its result to the editor text.
        /// </summary>
        /// <param name="context">
        /// The context describing the completed template operation.
        /// </param>
        public void TemplateAsynchronousCallback(
            IAsynchronousContext context /* in */
            ) /* AsynchronousCallback */
        {
            CheckDisposed();

            if (context == null)
                return;

            IClientData clientData = context.ClientData;

            if (clientData == null)
                return;

            bool append = false;

            if (clientData.Data is bool)
                append = (bool)clientData.Data;

            try
            {
                ModifyTextFromResult(
                    context.ReturnCode, context.Result,
                    context.ErrorLine, append); /* throw */
            }
            catch (Exception e)
            {
                LogOps.Complain(interpreter, ReturnCode.Error, e);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates the editor text from a script result, its return code, and
        /// error line, appending or replacing as requested.
        /// </summary>
        /// <param name="returnCode">
        /// The return code of the operation whose result is shown.
        /// </param>
        /// <param name="result">
        /// The result text to show.
        /// </param>
        /// <param name="errorLine">
        /// The error line number, or zero when none.
        /// </param>
        /// <param name="append">
        /// Non-zero to append; zero to replace.
        /// </param>
        public void ModifyTextFromResult(
            ReturnCode returnCode, /* in */
            Result result,         /* in */
            int errorLine,         /* in */
            bool append            /* in */
            )
        {
            CheckDisposed();

            switch (returnCode)
            {
                case ReturnCode.Ok:
                    {
                        //
                        // NOTE: Success, modify the currently loaded hot-key
                        //       script (i.e. either by appending to it or
                        //       replacing it).
                        //
                        if (append)
                        {
#if SCINTILLA
                            ScintillaOps.AppendText(txtText, result, false);
#else
                            WinFormsOps.AppendText(txtText, result, false);
#endif
                        }
                        else
                        {
                            WinFormsOps.SetText(txtText, result, false);
                        }

                        break;
                    }
                case ReturnCode.Error:
                    {
                        LogOps.LogOrComplain(interpreter,
                            Utility.FormatResult(returnCode,
                            Utility.FormatWrapOrNull(true, false, result),
                            errorLine));

                        break;
                    }
                case ReturnCode.Return:
                    {
                        goto case ReturnCode.Ok;
                    }
                case ReturnCode.Break:
                    {
                        WinFormsOps.SetText(txtText, result, false);
                        break;
                    }
                case ReturnCode.Continue:
                    {
#if SCINTILLA
                        ScintillaOps.AppendText(txtText, result, false);
#else
                        WinFormsOps.AppendText(txtText, result, false);
#endif
                        break;
                    }
                default:
                    {
                        goto case ReturnCode.Error;
                    }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Creates and shows the hot-key editor form, returning the edited
        /// hot-key.
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
        /// <param name="readOnly">
        /// Non-zero to open read-only.
        /// </param>
        /// <param name="advanced">
        /// Non-zero to start in advanced mode.
        /// </param>
        /// <param name="template">
        /// Non-zero to edit the hot-key's template.
        /// </param>
        /// <param name="hotKey">
        /// On input, the hot-key to edit; on output, the edited hot-key.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowEditor(
            IWin32Window owner,           /* in */
            Interpreter interpreter,      /* in */
            string varName,               /* in */
            int id,                       /* in */
            bool readOnly,                /* in */
            bool advanced,                /* in */
            bool template,                /* in */
            ref IHotKey hotKey,           /* in, out */
            ref Result error              /* out */
            )
        {
            bool saved = false;

            try
            {
#if SCINTILLA
                if (ScintillaOps.PreLoadNativeLibrary(
                        interpreter, true, ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }
#endif

                using (HotKeyEditForm form = new HotKeyEditForm(
                        id, interpreter, varName))
                {
                    form.ClearEditor();
                    form.LoadTemplateTypes(hotKey, readOnly, template);
                    form.ClearFlags();
                    form.LoadEditor(hotKey, readOnly);
                    form.SelectTemplateType(HotKeyTemplateType.Unknown);
                    form.SetMode(advanced);
                    form.SetDirty(false);

                    if (form.ShowDialog(owner) == form.ShouldSave())
                    {
                        if (readOnly)
                            return ReturnCode.Ok;

                        if (form.SaveEditor(
                                interpreter, ref hotKey,
                                ref error) == ReturnCode.Ok)
                        {
                            saved = true;
                            return ReturnCode.Ok;
                        }
                    }
                    else if (!readOnly && form.IsDirty())
                    {
                        error = "hot-key changes were not saved";
                    }
                    else
                    {
                        //
                        // NOTE: Either read-only mode is active -OR- the
                        //       hot-key that was being edited is unchanged
                        //       and only the cancel button could have been
                        //       used.  This is never an error.
                        //
                        return ReturnCode.Ok;
                    }
                }
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                ScriptOps.ResetCancel(interpreter, saved);
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
            {
                throw new ObjectDisposedException(
                    typeof(HotKeyEditForm).Name);
            }
#endif
        }
        #endregion
    }
}
