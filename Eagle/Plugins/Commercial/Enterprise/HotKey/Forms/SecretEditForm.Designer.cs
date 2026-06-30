/*
 * SecretEditForm.Designer.cs --
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
    internal sealed partial class SecretEditForm
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
            this.grpData = new System.Windows.Forms.GroupBox();
            this.chkShow = new System.Windows.Forms.CheckBox();
            this.chkHash = new System.Windows.Forms.CheckBox();
            this.txtData = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.grpVerificationCode = new System.Windows.Forms.GroupBox();
            this.txtVerificationCode = new System.Windows.Forms.TextBox();
            this.grpData.SuspendLayout();
            this.grpVerificationCode.SuspendLayout();
            this.SuspendLayout();
            //
            // grpData
            //
            this.grpData.Controls.Add(this.chkShow);
            this.grpData.Controls.Add(this.chkHash);
            this.grpData.Controls.Add(this.txtData);
            this.grpData.Location = new System.Drawing.Point(12, 12);
            this.grpData.Name = "grpData";
            this.grpData.Size = new System.Drawing.Size(700, 235);
            this.grpData.TabIndex = 0;
            this.grpData.TabStop = false;
            this.grpData.Text = "&Data";
            //
            // chkShow
            //
            this.chkShow.AutoSize = true;
            this.chkShow.Location = new System.Drawing.Point(6, 212);
            this.chkShow.Name = "chkShow";
            this.chkShow.Size = new System.Drawing.Size(86, 17);
            this.chkShow.TabIndex = 2;
            this.chkShow.Text = "&Show secret data...";
            this.chkShow.UseVisualStyleBackColor = true;
            //
            // chkHash
            //
            this.chkHash.AutoSize = true;
            this.chkHash.Location = new System.Drawing.Point(142, 212);
            this.chkHash.Name = "chkHash";
            this.chkHash.Size = new System.Drawing.Size(86, 17);
            this.chkHash.TabIndex = 2;
            this.chkHash.Text = "Compute &hash of data...";
            this.chkHash.UseVisualStyleBackColor = true;
            //
            // txtData
            //
            this.txtData.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtData.BackColor = System.Drawing.Color.Black;
            this.txtData.ForeColor = System.Drawing.Color.Black;
            this.txtData.Location = new System.Drawing.Point(6, 19);
            this.txtData.Multiline = true;
            this.txtData.Name = "txtData";
            this.txtData.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtData.Size = new System.Drawing.Size(688, 187);
            this.txtData.TabIndex = 1;
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(572, 268);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(140, 35);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // btnOk
            //
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(426, 268);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(140, 35);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "&Ok";
            this.btnOk.UseVisualStyleBackColor = true;
            //
            // grpVerificationCode
            //
            this.grpVerificationCode.Controls.Add(this.txtVerificationCode);
            this.grpVerificationCode.Location = new System.Drawing.Point(12, 253);
            this.grpVerificationCode.Name = "grpVerificationCode";
            this.grpVerificationCode.Size = new System.Drawing.Size(391, 58);
            this.grpVerificationCode.TabIndex = 3;
            this.grpVerificationCode.TabStop = false;
            this.grpVerificationCode.Text = "&Verification Code";
            //
            // txtVerificationCode
            //
            this.txtVerificationCode.Font = new System.Drawing.Font("Courier New", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtVerificationCode.Location = new System.Drawing.Point(6, 19);
            this.txtVerificationCode.MaxLength = 19;
            this.txtVerificationCode.Name = "txtVerificationCode";
            this.txtVerificationCode.ReadOnly = true;
            this.txtVerificationCode.Size = new System.Drawing.Size(379, 31);
            this.txtVerificationCode.TabIndex = 4;
            this.txtVerificationCode.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            //
            // SecretEditForm
            //
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(722, 321);
            this.Controls.Add(this.grpVerificationCode);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.grpData);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "SecretEditForm";
            this.Text = "Secret Data Editor";
            this.grpData.ResumeLayout(false);
            this.grpData.PerformLayout();
            this.grpVerificationCode.ResumeLayout(false);
            this.grpVerificationCode.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpData;
        private System.Windows.Forms.CheckBox chkShow;
        private System.Windows.Forms.CheckBox chkHash;
        private System.Windows.Forms.TextBox txtData;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.GroupBox grpVerificationCode;
        private System.Windows.Forms.TextBox txtVerificationCode;
    }
}

