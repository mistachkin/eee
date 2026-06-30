/*
 * ScriptEditForm.cs --
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
using HotKey.Components.Private;

namespace HotKey.Forms
{
    /// <summary>
    /// Implements the editor form for hot-key scripts, supporting syntax-aware
    /// editing, zoom, evaluation, and an isolated-evaluation option.
    /// </summary>
    [ObjectId("0737d46a-cb27-41df-b795-e9cc35ef17c9")]
    internal sealed partial class ScriptEditForm : BaseEditForm
    {
        #region Private Constants
        /// <summary>
        /// The prompt text shown before evaluating the script.
        /// </summary>
        private static readonly string EvaluateQuestionText =
            "This will evaluate the selected script text or " +
            "the entire script text if nothing is selected." +
            Environment.NewLine + Environment.NewLine +
            "Are you really sure?";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        #region Associated Eagle Interpreter
        /// <summary>
        /// The interpreter associated with the script editor.
        /// </summary>
        private Interpreter interpreter;
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

        #region Isolated Script Flag Support
        /// <summary>
        /// Non-zero to evaluate the script in an isolated interpreter.
        /// </summary>
        private bool isolated;
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="ScriptEditForm" /> with the specified
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
        private ScriptEditForm(
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

            SetIsolated(false);
            SetDirty(false);

            ///////////////////////////////////////////////////////////////////

            this.FormClosed += new FormClosedEventHandler(
                ScriptEditForm_FormClosed);

            this.Disposed += new EventHandler(ScriptEditForm_Disposed);

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
        private void ScriptEditForm_FormClosed(
            object sender,        /* in */
            FormClosedEventArgs e /* in */
            )
        {
            //
            // NOTE: If we have an interpreter, cancel any running hot-key
            //       script now.
            //
            if (interpreter != null)
            {
                ScriptOps.EnterPendingCancel();

                try
                {
                    ReturnCode cancelCode;
                    Result cancelError = null;

                    cancelCode = ScriptOps.CancelEvaluate(
                        interpreter, ref cancelError);

                    if (cancelCode != ReturnCode.Ok)
                    {
                        LogOps.Complain(
                            interpreter, cancelCode, cancelError);
                    }
                }
                finally
                {
                    ScriptOps.ExitPendingCancel();
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
        private void ScriptEditForm_Disposed(
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
                string text = null;

                if (
#if SCINTILLA
                    !ScintillaOps.GetText(txtText, true, ref text) ||
#else
                    !WinFormsOps.GetText(txtText, true, ref text) ||
#endif
                    (text == null))
                {
                    SaveEditor(ref text);
                }

                if (text != null)
                {
                    ReturnCode code;
                    Result result = null;

                    if (interpreter != null)
                    {
                        text = ScriptOps.GetTextToEvaluate(text);

                        code = ScriptOps.Evaluate(
                            interpreter, text, IsIsolated(), true,
                            true, false, ref result);
                    }
                    else
                    {
                        result = "invalid interpreter";
                        code = ReturnCode.Error;
                    }

                    if (code == ReturnCode.Ok)
                    {
                        LogOps.LogOrComplain(interpreter, String.Format(
                            "script result: {0}", Utility.FormatResult(
                            code, result)));
                    }
                    else
                    {
                        WinFormsOps.ShowResult(null, code, result);
                    }
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
        #region Isolated Script Flag Support
        /// <summary>
        /// Determines whether the script is evaluated in an isolated
        /// interpreter.
        /// </summary>
        /// <returns>
        /// Non-zero when isolated; otherwise, zero.
        /// </returns>
        private bool IsIsolated()
        {
            return isolated;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets whether the script is evaluated in an isolated interpreter.
        /// </summary>
        /// <param name="isolated">
        /// Non-zero to evaluate in an isolated interpreter.
        /// </param>
        private void SetIsolated(
            bool isolated /* in */
            )
        {
            this.isolated = isolated;
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

        #region Editor Load/Save Support
        //
        // NOTE: This method abstracts the different properties required to set
        //       a text box or a ScintillaNET control into the read-only mode.
        //
        /// <summary>
        /// Sets whether the script editor is read-only.
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
        /// Clears the script editor.
        /// </summary>
        protected override void ClearEditor()
        {
            txtText.Text = null;
            SetTextReadOnly(false);
            btnSave.Enabled = true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the supplied script into the editor.
        /// </summary>
        /// <param name="text">
        /// The script to load.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero to load read-only.
        /// </param>
        protected override void LoadEditor(
            string text,  /* in */
            bool readOnly /* in */
            )
        {
            txtText.Text = text;
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

            btnSave.Enabled = !readOnly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the script editor content into the supplied reference
        /// argument.
        /// </summary>
        /// <param name="text">
        /// On output, receives the editor content.
        /// </param>
        protected override void SaveEditor(
            ref string text /* out */
            )
        {
            text = txtText.Text;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Creates and shows the script editor form, returning the edited
        /// script.
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
        /// <param name="isolated">
        /// Non-zero to evaluate in an isolated interpreter.
        /// </param>
        /// <param name="text">
        /// On input, the initial script; on output, the edited script.
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
            bool isolated,                /* in */
            ref string text,              /* in, out */
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

                using (ScriptEditForm form = new ScriptEditForm(
                        id, interpreter, varName))
                {
                    form.ClearEditor();
                    form.LoadEditor(text, readOnly);
                    form.SetIsolated(isolated);
                    form.SetDirty(false);

                    if (form.ShowDialog(owner) == form.ShouldSave())
                    {
                        if (readOnly)
                            return ReturnCode.Ok;

                        form.SaveEditor(ref text);
                        saved = true;

                        return ReturnCode.Ok;
                    }
                    else if (!readOnly && form.IsDirty())
                    {
                        error = "script changes were not saved";
                    }
                    else
                    {
                        //
                        // NOTE: Either read-only mode is active -OR- the
                        //       script that was being edited is unchanged
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
                    typeof(ScriptEditForm).Name);
            }
#endif
        }
        #endregion
    }
}
