/*
 * BaseEditForm.cs --
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
using HotKey.Components.Private;

namespace HotKey.Forms
{
    /// <summary>
    /// Provides the abstract base class for the editor forms.  It tracks the
    /// unsaved-changes (dirty) and closed states, prompts to save on close
    /// when dirty, and defines the abstract editor load/save operations.
    /// </summary>
    [ObjectId("414933d4-4549-4c44-bd06-8d260dc203f1")]
    internal abstract class BaseEditForm : BaseForm
    {
        #region Private Constants
        /// <summary>
        /// The prompt text shown when closing a form that has unsaved changes.
        /// </summary>
        private static readonly string ClosingQuestionText =
            "It appears there are unsaved changes to this data." +
            Environment.NewLine + Environment.NewLine +
            "Save changes before closing?";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        #region Dirty (i.e. Unsaved Data) Flag
        /// <summary>
        /// Non-zero when the form has unsaved changes.
        /// </summary>
        private bool dirty;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Closed (i.e. Form Gone) Flag
        /// <summary>
        /// Non-zero when the form has been closed.
        /// </summary>
        private bool closed;
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Constructs a new <see cref="BaseEditForm" /> and wires up its
        /// closing and closed event handlers.
        /// </summary>
        /// <param name="id">
        /// The form id.
        /// </param>
        /// <param name="interpreter">
        /// The interpreter the form is associated with.
        /// </param>
        /// <param name="varName">
        /// The variable that receives the form id, if any.
        /// </param>
        public BaseEditForm(
            int id,                  /* in */
            Interpreter interpreter, /* in */
            string varName           /* in */
            )
            : base(id, interpreter, varName)
        {
            this.FormClosing += new FormClosingEventHandler(
                BaseEditForm_FormClosing);

            this.FormClosed += new FormClosedEventHandler(
                BaseEditForm_FormClosed);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Event Handlers
        #region Form Event Handlers
        /// <summary>
        /// Handles the form-closing event, prompting the user to save when the
        /// form is dirty and changes are not already being saved.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The closing event data.
        /// </param>
        private void BaseEditForm_FormClosing(
            object sender,         /* in */
            FormClosingEventArgs e /* in */
            )
        {
            if (IsDirty() && !WillSave())
            {
                //
                // NOTE: Dirty flag is set for this form, prompt the user
                //       to save changes now.
                //
                switch (WinFormsOps.YesOrNo(this, ClosingQuestionText))
                {
                    case DialogResult.Yes:
                        {
                            //
                            // NOTE: Just set the dialog result to OK;
                            //       this should cause the changes to
                            //       be saved.  If this does not cause
                            //       the changes to be saved, there is
                            //       not much we can do about it.
                            //
                            SetSave(true);
                            break;
                        }
                    case DialogResult.No:
                        {
                            //
                            // NOTE: Just in case we fail to actually
                            //       close the form, prevent further
                            //       prompts to save by resetting the
                            //       dirty flag now.
                            //
                            SetDirty(false);
                            break;
                        }
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the form-closed event, marking the form as closed.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The closed event data.
        /// </param>
        private void BaseEditForm_FormClosed(
            object sender,        /* in */
            FormClosedEventArgs e /* in */
            )
        {
            SetClosed(true);
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Protected Methods
        #region Generic Save Support
        /// <summary>
        /// Gets the dialog result that indicates the data should be saved.
        /// </summary>
        /// <returns>
        /// The save dialog result.
        /// </returns>
        protected virtual DialogResult ShouldSave()
        {
            return DialogResult.OK;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the dialog result that indicates the data should not be saved.
        /// </summary>
        /// <returns>
        /// The do-not-save dialog result.
        /// </returns>
        protected virtual DialogResult ShouldNotSave()
        {
            return DialogResult.Cancel;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the form's dialog result to indicate whether the data should
        /// be saved.
        /// </summary>
        /// <param name="save">
        /// Non-zero to indicate the data should be saved.
        /// </param>
        protected virtual void SetSave(
            bool save /* in */
            )
        {
            this.DialogResult = save ? ShouldSave() : ShouldNotSave();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the form's current dialog result indicates the
        /// data will be saved.
        /// </summary>
        /// <returns>
        /// Non-zero when the data will be saved; otherwise, zero.
        /// </returns>
        protected virtual bool WillSave()
        {
            return (this.DialogResult == ShouldSave());
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Dirty (i.e. Unsaved Data) Flag Support
        /// <summary>
        /// Determines whether the form has unsaved changes.
        /// </summary>
        /// <returns>
        /// Non-zero when the form is dirty; otherwise, zero.
        /// </returns>
        protected virtual bool IsDirty()
        {
            return dirty;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets whether the form has unsaved changes.
        /// </summary>
        /// <param name="dirty">
        /// Non-zero to mark the form dirty.
        /// </param>
        protected virtual void SetDirty(
            bool dirty /* in */
            )
        {
            this.dirty = dirty;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Closed (i.e. Form Gone) Flag Support
        /// <summary>
        /// Determines whether the form has been closed.
        /// </summary>
        /// <returns>
        /// Non-zero when the form is closed; otherwise, zero.
        /// </returns>
        protected virtual bool IsClosed()
        {
            return closed;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets whether the form has been closed.
        /// </summary>
        /// <param name="closed">
        /// Non-zero to mark the form closed.
        /// </param>
        protected virtual void SetClosed(
            bool closed /* in */
            )
        {
            this.closed = closed;
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Abstract Editor Load/Save Support
        /// <summary>
        /// Sets whether the editor's text is read-only.
        /// </summary>
        /// <param name="readOnly">
        /// Non-zero to make the editor read-only.
        /// </param>
        protected abstract void SetTextReadOnly(
            bool readOnly /* in */
        );

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the editor's content.
        /// </summary>
        protected abstract void ClearEditor();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the supplied text into the editor.
        /// </summary>
        /// <param name="text">
        /// The text to load.
        /// </param>
        /// <param name="readOnly">
        /// Non-zero to load the editor read-only.
        /// </param>
        protected abstract void LoadEditor(
            string text,  /* in */
            bool readOnly /* in */
        );

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the editor's content into the supplied reference argument.
        /// </summary>
        /// <param name="text">
        /// On output, receives the editor content.
        /// </param>
        protected abstract void SaveEditor(
            ref string text /* out */
        );
        #endregion
    }
}
