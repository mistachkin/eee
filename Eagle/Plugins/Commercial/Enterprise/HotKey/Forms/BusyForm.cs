/*
 * BusyForm.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Threading;
using System.Windows.Forms;
using Eagle._Attributes;
using Eagle._Components.Public;
using HotKey.Components.Private;

namespace HotKey.Forms
{
    /// <summary>
    /// Implements the modal "busy" indicator form, which displays a title and
    /// elapsed time with a cancel button while a long operation runs.
    /// </summary>
    [ObjectId("62e54abe-b385-482b-ba57-0168dcf02947")]
    internal sealed partial class BusyForm : BaseForm
    {
        #region Private Constants
        /// <summary>
        /// The default title shown by the busy form.
        /// </summary>
        private const string DefaultTitle = "Busy, please wait...";

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The prompt text shown when the user attempts to cancel.
        /// </summary>
        private const string CancelQuestionText =
            "Really cancel all running hot-key template scripts?";
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        /// <summary>
        /// The title displayed by the busy form.
        /// </summary>
        private string title;
        /// <summary>
        /// Non-zero once the busy form has started its timer.
        /// </summary>
        private DateTime started;
        /// <summary>
        /// The interpreter associated with the busy form.
        /// </summary>
        private Interpreter interpreter;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Constructs a new <see cref="BusyForm" /> with the specified id,
        /// interpreter, and result variable name.
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
        private BusyForm(
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

            this.Shown += new EventHandler(BusyForm_Shown);

            this.FormClosing += new FormClosingEventHandler(
                BusyForm_FormClosing);

            this.Disposed += new EventHandler(BusyForm_Disposed);

            btnCancel.Click += new System.EventHandler(btnCancel_Click);

            ///////////////////////////////////////////////////////////////////

            tmrBusy.Tick += new EventHandler(tmrBusy_Tick);
            tmrBusy.Start();
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
        private void BusyForm_Shown(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            started = Utility.GetNow();
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
        private void BusyForm_FormClosing(
            object sender,         /* in */
            FormClosingEventArgs e /* in */
            )
        {
            tmrBusy.Stop();
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
        private void BusyForm_Disposed(
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
            if (WinFormsOps.YesOrNo(
                    this, CancelQuestionText) == DialogResult.Yes)
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
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles the busy-timer tick event.
        /// </summary>
        /// <param name="sender">
        /// The source of the event.
        /// </param>
        /// <param name="e">
        /// The event data.
        /// </param>
        private void tmrBusy_Tick(
            object sender, /* in */
            EventArgs e    /* in */
            )
        {
            prbBusy.PerformStep();
            prbBusy.Refresh();

            if (prbBusy.Value == prbBusy.Maximum)
                prbBusy.Value = prbBusy.Minimum;

            /* IGNORED */
            ShowElapsed();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Sets the title displayed by the busy form.
        /// </summary>
        /// <param name="title">
        /// The title to display.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool SetTitle(
            string title /* in */
            )
        {
            CheckDisposed();

            bool result = WinFormsOps.SetText(this, title, false);

            if (result)
                this.title = title;

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates the displayed elapsed time.
        /// </summary>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        public bool ShowElapsed()
        {
            CheckDisposed();

            DateTime now = Utility.GetNow();

            return WinFormsOps.SetText(this, String.Format("{0} - {1}",
                title, now.Subtract((DateTime)started)), false);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Creates and shows a busy form for the specified operation.
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
        /// <param name="title">
        /// The title to display.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another <see
        /// cref="ReturnCode" /> value that indicates the type of failure.
        /// </returns>
        public static ReturnCode ShowBusy(
            int id,                  /* in */
            Interpreter interpreter, /* in */
            string varName,          /* in */
            string title,            /* in */
            ref Result error         /* in */
            )
        {
            try
            {
                Thread thread = Engine.CreateThread(interpreter, delegate()
                {
                    using (BusyForm form = new BusyForm(
                            id, interpreter, varName))
                    {
                        form.SetTitle((title != null) ? title : DefaultTitle);
                        Application.Run(form);
                    }
                }, 0, true, false, true);

                thread.Name = String.Format("{0}: {1}",
                    typeof(BusyForm).FullName, interpreter);

                thread.Start();
                return ReturnCode.Ok;
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
                throw new ObjectDisposedException(typeof(BusyForm).Name);
#endif
        }
        #endregion
    }
}
