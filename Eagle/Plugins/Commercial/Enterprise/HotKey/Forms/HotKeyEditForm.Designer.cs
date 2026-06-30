/*
 * HotKeyEditForm.Designer.cs --
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
    internal sealed partial class HotKeyEditForm
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
            this.grpKeys = new System.Windows.Forms.GroupBox();
            this.txtKeys = new System.Windows.Forms.TextBox();
            this.btnSelectKeys = new System.Windows.Forms.Button();
            this.grpFlags = new System.Windows.Forms.GroupBox();
            this.lstFlag = new System.Windows.Forms.CheckedListBox();
            this.grpTemplate = new System.Windows.Forms.GroupBox();
            this.btnTemplate = new System.Windows.Forms.Button();
            this.cboTemplate = new System.Windows.Forms.ComboBox();
            this.grpText = new System.Windows.Forms.GroupBox();
#if SCINTILLA
            this.txtText = new ScintillaNET.Scintilla();
#else
            this.txtText = new System.Windows.Forms.TextBox();
#endif
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pnlSave = new System.Windows.Forms.Panel();
            this.pnlMode = new System.Windows.Forms.Panel();
            this.btnMode = new System.Windows.Forms.Button();
            this.btnEvaluate = new System.Windows.Forms.Button();
            this.grpKeys.SuspendLayout();
            this.grpFlags.SuspendLayout();
            this.grpTemplate.SuspendLayout();
            this.grpText.SuspendLayout();
#if SCINTILLA && !SCINTILLA_30
            ((System.ComponentModel.ISupportInitialize)(this.txtText)).BeginInit();
#endif
            this.pnlSave.SuspendLayout();
            this.pnlMode.SuspendLayout();
            this.SuspendLayout();
            //
            // grpKeys
            //
            this.grpKeys.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.grpKeys.Controls.Add(this.txtKeys);
            this.grpKeys.Controls.Add(this.btnSelectKeys);
            this.grpKeys.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpKeys.Location = new System.Drawing.Point(12, 12);
            this.grpKeys.Name = "grpKeys";
            this.grpKeys.Size = new System.Drawing.Size(676, 61);
            this.grpKeys.TabIndex = 0;
            this.grpKeys.TabStop = false;
            this.grpKeys.Text = "&Keys";
            //
            // txtKeys
            //
            this.txtKeys.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtKeys.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtKeys.Location = new System.Drawing.Point(9, 24);
            this.txtKeys.Name = "txtKeys";
            this.txtKeys.ReadOnly = true;
            this.txtKeys.Size = new System.Drawing.Size(606, 26);
            this.txtKeys.TabIndex = 0;
            //
            // btnSelectKeys
            //
            this.btnSelectKeys.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSelectKeys.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSelectKeys.Location = new System.Drawing.Point(621, 24);
            this.btnSelectKeys.Name = "btnSelectKeys";
            this.btnSelectKeys.Size = new System.Drawing.Size(46, 26);
            this.btnSelectKeys.TabIndex = 1;
            this.btnSelectKeys.Text = "...";
            //
            // grpFlags
            //
            this.grpFlags.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFlags.Controls.Add(this.lstFlag);
            this.grpFlags.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpFlags.Location = new System.Drawing.Point(12, 79);
            this.grpFlags.Name = "grpFlags";
            this.grpFlags.Size = new System.Drawing.Size(676, 144);
            this.grpFlags.TabIndex = 1;
            this.grpFlags.TabStop = false;
            this.grpFlags.Text = "&Flags";
            //
            // lstFlag
            //
            this.lstFlag.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lstFlag.ColumnWidth = 325;
            this.lstFlag.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstFlag.Location = new System.Drawing.Point(9, 24);
            this.lstFlag.MultiColumn = true;
            this.lstFlag.Name = "lstFlag";
            this.lstFlag.Size = new System.Drawing.Size(658, 109);
            this.lstFlag.TabIndex = 0;
            //
            // grpTemplate
            //
            this.grpTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.grpTemplate.Controls.Add(this.btnTemplate);
            this.grpTemplate.Controls.Add(this.cboTemplate);
            this.grpTemplate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpTemplate.Location = new System.Drawing.Point(12, 229);
            this.grpTemplate.Name = "grpTemplate";
            this.grpTemplate.Size = new System.Drawing.Size(676, 61);
            this.grpTemplate.TabIndex = 2;
            this.grpTemplate.TabStop = false;
            this.grpTemplate.Text = "T&emplate";
            //
            // btnTemplate
            //
            this.btnTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnTemplate.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnTemplate.Location = new System.Drawing.Point(621, 24);
            this.btnTemplate.Name = "btnTemplate";
            this.btnTemplate.Size = new System.Drawing.Size(46, 26);
            this.btnTemplate.TabIndex = 1;
            this.btnTemplate.Text = "=>";
            //
            // cboTemplate
            //
            this.cboTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.cboTemplate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTemplate.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboTemplate.Location = new System.Drawing.Point(9, 24);
            this.cboTemplate.Name = "cboTemplate";
            this.cboTemplate.Size = new System.Drawing.Size(606, 26);
            this.cboTemplate.TabIndex = 0;
            //
            // grpText
            //
            this.grpText.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.grpText.Controls.Add(this.txtText);
            this.grpText.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpText.Location = new System.Drawing.Point(12, 296);
            this.grpText.Name = "grpText";
            this.grpText.Size = new System.Drawing.Size(676, 271);
            this.grpText.TabIndex = 3;
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
            this.pnlSave.Location = new System.Drawing.Point(376, 580);
            this.pnlSave.Name = "pnlSave";
            this.pnlSave.Size = new System.Drawing.Size(324, 47);
            this.pnlSave.TabIndex = 5;
            //
            // pnlMode
            //
            this.pnlMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.pnlMode.Controls.Add(this.btnEvaluate);
            this.pnlMode.Controls.Add(this.btnMode);
            this.pnlMode.Location = new System.Drawing.Point(0, 580);
            this.pnlMode.Name = "pnlMode";
            this.pnlMode.Size = new System.Drawing.Size(324, 47);
            this.pnlMode.TabIndex = 4;
            //
            // btnMode
            //
            this.btnMode.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnMode.Location = new System.Drawing.Point(12, 0);
            this.btnMode.Name = "btnMode";
            this.btnMode.Size = new System.Drawing.Size(128, 34);
            this.btnMode.TabIndex = 0;
            this.btnMode.Text = "&Basic";
            //
            // btnEvaluate
            //
            this.btnEvaluate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnEvaluate.Location = new System.Drawing.Point(178, 0);
            this.btnEvaluate.Name = "btnEvaluate";
            this.btnEvaluate.Size = new System.Drawing.Size(128, 34);
            this.btnEvaluate.TabIndex = 1;
            this.btnEvaluate.Text = "E&valuate";
            //
            // HotKeyEditForm
            //
            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(700, 627);
            this.Controls.Add(this.pnlSave);
            this.Controls.Add(this.grpText);
            this.Controls.Add(this.grpTemplate);
            this.Controls.Add(this.grpFlags);
            this.Controls.Add(this.grpKeys);
            this.Controls.Add(this.pnlMode);
            this.MinimumSize = new System.Drawing.Size(716, 655);
            this.Name = "HotKeyEditForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hot-Key Editor";
            this.grpKeys.ResumeLayout(false);
            this.grpKeys.PerformLayout();
            this.grpFlags.ResumeLayout(false);
            this.grpTemplate.ResumeLayout(false);
            this.grpText.ResumeLayout(false);
#if SCINTILLA && !SCINTILLA_30
            ((System.ComponentModel.ISupportInitialize)(this.txtText)).EndInit();
#endif
            this.pnlSave.ResumeLayout(false);
            this.pnlMode.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        private System.Windows.Forms.GroupBox grpKeys;
        private System.Windows.Forms.GroupBox grpFlags;
        private System.Windows.Forms.Button btnSelectKeys;
        private System.Windows.Forms.TextBox txtKeys;
        private System.Windows.Forms.GroupBox grpText;
#if SCINTILLA
        private ScintillaNET.Scintilla txtText;
#else
        private System.Windows.Forms.TextBox txtText;
#endif
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox grpTemplate;
        private System.Windows.Forms.ComboBox cboTemplate;
        private System.Windows.Forms.Button btnTemplate;
        private System.Windows.Forms.CheckedListBox lstFlag;
        private System.Windows.Forms.Panel pnlSave;
        private System.Windows.Forms.Panel pnlMode;
        private System.Windows.Forms.Button btnEvaluate;
        private System.Windows.Forms.Button btnMode;
    }
}
