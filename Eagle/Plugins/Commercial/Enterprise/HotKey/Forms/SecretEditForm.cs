/*
 * SecretEditForm.cs --
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
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using HotKey.Components.Private;

namespace HotKey.Forms
{
    /// <summary>
    /// Implements the editor form for secret (masked) text, showing a
    /// verification code and supporting show/hide of the value.
    /// </summary>
    [ObjectId("353f2a72-f135-420b-b3a2-58525f932dcc")]
    internal sealed partial class SecretEditForm : BaseEditForm
    {
        #region Private Constants
        //
        // NOTE: This is the minimum number of seconds to wait between
        //       verification code calculations.
        //
        /// <summary>
        /// The minimum number of seconds that must elapse before the
        /// verification code is recomputed.
        /// </summary>
        private const int minimumSeconds = 30;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: The last time a verification code was calculated for
        //       this form.
        //
        /// <summary>
        /// The tick count of the last verification-code update.
        /// </summary>
        private long lastTicks = 0;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="SecretEditForm" /> with the specified
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
        private SecretEditForm(
            int id,                  /* in */
            Interpreter interpreter, /* in */
            string varName           /* in */
            )
            : base(id, interpreter, varName)
        {
            InitializeComponent();

            ///////////////////////////////////////////////////////////////////

            txtData.TextChanged += new EventHandler(txtData_TextChanged);
            txtData.KeyPress += new KeyPressEventHandler(txtData_KeyPress);

            chkShow.CheckStateChanged += new EventHandler(
                chkShow_CheckStateChanged);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Event Handlers
        #region Data Event Handlers
        /// <summary>
        /// Handles the data text-changed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void txtData_TextChanged(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            SetDirty(true);

            txtVerificationCode.Text = null;

            Utility.QueueUserWorkItem(new WaitCallback(
                ResetVerificationCode), txtData.Text,
                QueueFlags.Default);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the data key-press event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void txtData_KeyPress(
            object sender,      /* in */
            KeyPressEventArgs e /* in */
            )
        {
            if ((chkHash.CheckState == CheckState.Checked) &&
                WinFormsOps.HashAndSetText(
                    txtData, txtData.Text, e.KeyChar, false))
            {
                e.Handled = true;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Button Event Handlers
        /// <summary>
        /// Handles the show check-state-changed event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void chkShow_CheckStateChanged(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            SetVisible(chkShow.CheckState == CheckState.Checked);
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        #region Editor Load/Save Support
        /// <summary>
        /// Sets whether the secret text editor is read-only.
        /// </summary>
        /// <param name="readOnly">
        /// Non-zero to make the editor read-only.
        /// </param>
        protected override void SetTextReadOnly(
            bool readOnly /* in */
            )
        {
            txtData.ReadOnly = readOnly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the secret text editor.
        /// </summary>
        protected override void ClearEditor()
        {
            txtData.Text = null;

            SetTextReadOnly(false);

            btnOk.Enabled = true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads the supplied secret text into the editor.
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
            txtData.Text = text;
            txtData.SelectionStart = 0;
            txtData.SelectionLength = 0;

            SetTextReadOnly(readOnly);

            ///////////////////////////////////////////////////////////////////

            btnOk.Enabled = !readOnly;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the secret text editor content into the supplied reference
        /// argument.
        /// </summary>
        /// <param name="text">
        /// On output, receives the editor content.
        /// </param>
        protected override void SaveEditor(
            ref string text /* out */
            )
        {
            text = txtData.Text;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Secret Support
        /// <summary>
        /// Sets whether the secret text is shown in clear rather than masked.
        /// </summary>
        /// <param name="visible">
        /// Non-zero to show the text in clear.
        /// </param>
        private void SetVisible(
            bool visible /* in */
            )
        {
            if (visible)
            {
                txtData.ForeColor = SystemColors.WindowText;

                if (txtData.ReadOnly)
                    txtData.BackColor = SystemColors.Control;
                else
                    txtData.BackColor = SystemColors.Window;
            }
            else
            {
                txtData.ForeColor = SystemColors.WindowText;
                txtData.BackColor = SystemColors.WindowText;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Verification Code Support
        /* System.Threading.WaitCallback */
        /// <summary>
        /// Recomputes and updates the displayed verification code for the
        /// current secret text.
        /// </summary>
        /// <param name="state">
        /// The state object passed by the timer callback, if any.
        /// </param>
        private void ResetVerificationCode(
            object state /* in */
            )
        {
            long last = Interlocked.CompareExchange(
                ref lastTicks, 0, 0);

            long now = Utility.GetUtcNowTicks();

            if (last != 0)
            {
                TimeSpan span = new DateTime(
                    now, DateTimeKind.Utc).Subtract(
                    new DateTime(last, DateTimeKind.Utc));

                if (span.TotalSeconds < minimumSeconds)
                {
                    BeginInvoke(new DelegateWithNoArgs(delegate()
                    {
                        txtVerificationCode.Text = "Too fast...";
                    }));

                    return;
                }
            }

            if (Interlocked.CompareExchange(
                    ref lastTicks, now, last) != last)
            {
                return;
            }

            try
            {
                string text = state as string;

                if (text != null)
                {
                    string code = VCodeOps.Format(
                        VCodeOps.Calculate(Encoding.UTF8, text));

                    if (!IsClosed())
                    {
                        Invoke(new DelegateWithNoArgs(delegate()
                        {
                            txtVerificationCode.Text = code;
                        }));
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Thread.ResetAbort();
            }
            catch (ThreadInterruptedException)
            {
                // do nothing.
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(SecretEditForm).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);
            }
        }
        #endregion
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Creates and shows the secret editor form, returning the edited
        /// text.
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
        /// <param name="visible">
        /// Non-zero to show the text in clear initially.
        /// </param>
        /// <param name="text">
        /// On input, the initial text; on output, the edited text.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowEditor(
            IWin32Window owner,      /* in */
            Interpreter interpreter, /* in */
            string varName,          /* in */
            int id,                  /* in */
            bool readOnly,           /* in */
            bool visible,            /* in */
            ref string text,         /* in, out */
            ref Result error         /* out */
            )
        {
            try
            {
                using (SecretEditForm form = new SecretEditForm(
                        id, interpreter, varName))
                {
                    form.ClearEditor();
                    form.LoadEditor(text, readOnly);
                    form.SetDirty(false);
                    form.SetVisible(visible);

                    if (form.ShowDialog(owner) == form.ShouldSave())
                    {
                        if (readOnly)
                            return ReturnCode.Ok;

                        form.SaveEditor(ref text);
                        return ReturnCode.Ok;
                    }
                    else if (!readOnly && form.IsDirty())
                    {
                        error = "data changes were not saved";
                    }
                    else
                    {
                        //
                        // NOTE: Either read-only mode is active -OR- the
                        //       data that was being edited is unchanged
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

            return ReturnCode.Error;
        }
        #endregion
    }
}
