/*
 * BusyForm.Designer.cs --
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
    internal sealed partial class BusyForm
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
            this.components = new System.ComponentModel.Container();
            this.tmrBusy = new System.Windows.Forms.Timer(this.components);
            this.prbBusy = new System.Windows.Forms.ProgressBar();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // prbBusy
            //
            this.prbBusy.Location = new System.Drawing.Point(12, 12);
            this.prbBusy.Name = "prbBusy";
            this.prbBusy.Size = new System.Drawing.Size(526, 44);
            this.prbBusy.Step = 1;
            this.prbBusy.TabIndex = 0;
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCancel.Location = new System.Drawing.Point(205, 65);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(140, 35);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // BusyForm
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(550, 112);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.prbBusy);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "BusyForm";
            this.Text = "Busy...";
            this.ResumeLayout(false);
        }
        #endregion

        private System.Windows.Forms.ProgressBar prbBusy;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Timer tmrBusy;
    }
}
