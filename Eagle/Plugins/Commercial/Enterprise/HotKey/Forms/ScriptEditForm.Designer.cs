/*
 * ScriptEditForm.Designer.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

namespace HotKey.Forms
{
    internal sealed partial class ScriptEditForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.grpText = new System.Windows.Forms.GroupBox();
#if SCINTILLA
            this.txtText = new ScintillaNET.Scintilla();
#else
            this.txtText = new System.Windows.Forms.TextBox();
#endif
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pnlSave = new System.Windows.Forms.Panel();
            this.pnlEvaluate = new System.Windows.Forms.Panel();
            this.btnEvaluate = new System.Windows.Forms.Button();
            this.grpText.SuspendLayout();
#if SCINTILLA && !SCINTILLA_30
            ((System.ComponentModel.ISupportInitialize)(this.txtText)).BeginInit();
#endif
            this.pnlSave.SuspendLayout();
            this.pnlEvaluate.SuspendLayout();
            this.SuspendLayout();
            //
            // grpText
            //
            this.grpText.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.grpText.Controls.Add(this.txtText);
            this.grpText.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpText.Location = new System.Drawing.Point(12, 12);
            this.grpText.Name = "grpText";
            this.grpText.Size = new System.Drawing.Size(676, 271);
            this.grpText.TabIndex = 0;
            this.grpText.TabStop = false;
            this.grpText.Text = "&Text";
            //
            // txtText
            //
            this.txtText.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtText.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtText.Location = new System.Drawing.Point(9, 24);
#if !SCINTILLA
            this.txtText.AcceptsReturn = true;
            this.txtText.MaxLength = 0;
            this.txtText.Multiline = true;
            this.txtText.ScrollBars = System.Windows.Forms.ScrollBars.Both;
#endif
            this.txtText.Name = "txtText";
            this.txtText.Size = new System.Drawing.Size(658, 236);
            this.txtText.TabIndex = 0;
            //
            // btnSave
            //
            this.btnSave.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSave.Location = new System.Drawing.Point(18, 0);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(128, 34);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "&Save";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCancel.Location = new System.Drawing.Point(184, 0);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(128, 34);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "&Cancel";
            //
            // pnlSave
            //
            this.pnlSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlSave.Controls.Add(this.btnCancel);
            this.pnlSave.Controls.Add(this.btnSave);
            this.pnlSave.Location = new System.Drawing.Point(376, 296);
            this.pnlSave.Name = "pnlSave";
            this.pnlSave.Size = new System.Drawing.Size(324, 47);
            this.pnlSave.TabIndex = 2;
            //
            // pnlEvaluate
            //
            this.pnlEvaluate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.pnlEvaluate.Controls.Add(this.btnEvaluate);
            this.pnlEvaluate.Location = new System.Drawing.Point(0, 296);
            this.pnlEvaluate.Name = "pnlEvaluate";
            this.pnlEvaluate.Size = new System.Drawing.Size(324, 47);
            this.pnlEvaluate.TabIndex = 1;
            //
            // btnEvaluate
            //
            this.btnEvaluate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnEvaluate.Location = new System.Drawing.Point(18, 0);
            this.btnEvaluate.Name = "btnEvaluate";
            this.btnEvaluate.Size = new System.Drawing.Size(128, 34);
            this.btnEvaluate.TabIndex = 0;
            this.btnEvaluate.Text = "&Evaluate";
            //
            // ScriptEditForm
            //
            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(700, 343);
            this.Controls.Add(this.pnlSave);
            this.Controls.Add(this.grpText);
            this.Controls.Add(this.pnlEvaluate);
            this.MinimumSize = new System.Drawing.Size(716, 371);
            this.Name = "ScriptEditForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Script Editor";
            this.grpText.ResumeLayout(false);
#if SCINTILLA && !SCINTILLA_30
            ((System.ComponentModel.ISupportInitialize)(this.txtText)).EndInit();
#endif
            this.pnlSave.ResumeLayout(false);
            this.pnlEvaluate.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        private System.Windows.Forms.GroupBox grpText;
#if SCINTILLA
        private ScintillaNET.Scintilla txtText;
#else
        private System.Windows.Forms.TextBox txtText;
#endif
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel pnlSave;
        private System.Windows.Forms.Panel pnlEvaluate;
        private System.Windows.Forms.Button btnEvaluate;
    }
}
