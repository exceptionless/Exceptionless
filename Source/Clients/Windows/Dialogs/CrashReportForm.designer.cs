namespace Exceptionless.Dialogs
{
    sealed partial class CrashReportForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CrashReportForm));
            this.DescriptionTextBox = new System.Windows.Forms.TextBox();
            this.EmailAddressTextBox = new System.Windows.Forms.TextBox();
            this.EmailAddressLabel = new System.Windows.Forms.Label();
            this.DescriptionLabel = new System.Windows.Forms.Label();
            this.titleGroupBox = new System.Windows.Forms.GroupBox();
            this.InformationHeaderPictureBox = new System.Windows.Forms.PictureBox();
            this.InformationHeaderLabel = new System.Windows.Forms.Label();
            this.InformationBodyLabel = new System.Windows.Forms.Label();
            this.buttonGroupBox = new System.Windows.Forms.GroupBox();
            this.ExitButton = new System.Windows.Forms.Button();
            this.SendReportButton = new System.Windows.Forms.Button();
            this.titleGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.InformationHeaderPictureBox)).BeginInit();
            this.buttonGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // DescriptionTextBox
            // 
            this.DescriptionTextBox.AcceptsReturn = true;
            this.DescriptionTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DescriptionTextBox.Location = new System.Drawing.Point(12, 269);
            this.DescriptionTextBox.MaxLength = 5000;
            this.DescriptionTextBox.Multiline = true;
            this.DescriptionTextBox.Name = "DescriptionTextBox";
            this.DescriptionTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.DescriptionTextBox.Size = new System.Drawing.Size(372, 120);
            this.DescriptionTextBox.TabIndex = 5;
            // 
            // EmailAddressTextBox
            // 
            this.EmailAddressTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.EmailAddressTextBox.Location = new System.Drawing.Point(12, 223);
            this.EmailAddressTextBox.MaxLength = 128;
            this.EmailAddressTextBox.Name = "EmailAddressTextBox";
            this.EmailAddressTextBox.Size = new System.Drawing.Size(287, 20);
            this.EmailAddressTextBox.TabIndex = 3;
            // 
            // EmailAddressLabel
            // 
            this.EmailAddressLabel.Location = new System.Drawing.Point(9, 204);
            this.EmailAddressLabel.Name = "EmailAddressLabel";
            this.EmailAddressLabel.Size = new System.Drawing.Size(375, 16);
            this.EmailAddressLabel.TabIndex = 2;
            this.EmailAddressLabel.Text = "Your &email address (optional):";
            // 
            // DescriptionLabel
            // 
            this.DescriptionLabel.Location = new System.Drawing.Point(9, 250);
            this.DescriptionLabel.Name = "DescriptionLabel";
            this.DescriptionLabel.Size = new System.Drawing.Size(332, 16);
            this.DescriptionLabel.TabIndex = 4;
            this.DescriptionLabel.Text = "&Describe what you were doing when the error occurred (optional): ";
            // 
            // titleGroupBox
            // 
            this.titleGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.titleGroupBox.BackColor = System.Drawing.SystemColors.Window;
            this.titleGroupBox.Controls.Add(this.InformationHeaderPictureBox);
            this.titleGroupBox.Controls.Add(this.InformationHeaderLabel);
            this.titleGroupBox.Location = new System.Drawing.Point(-2, -7);
            this.titleGroupBox.Name = "titleGroupBox";
            this.titleGroupBox.Size = new System.Drawing.Size(400, 64);
            this.titleGroupBox.TabIndex = 0;
            this.titleGroupBox.TabStop = false;
            // 
            // InformationHeaderPictureBox
            // 
            this.InformationHeaderPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.InformationHeaderPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("InformationHeaderPictureBox.Image")));
            this.InformationHeaderPictureBox.Location = new System.Drawing.Point(351, 19);
            this.InformationHeaderPictureBox.Name = "InformationHeaderPictureBox";
            this.InformationHeaderPictureBox.Size = new System.Drawing.Size(35, 35);
            this.InformationHeaderPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.InformationHeaderPictureBox.TabIndex = 1;
            this.InformationHeaderPictureBox.TabStop = false;
            // 
            // InformationHeaderLabel
            // 
            this.InformationHeaderLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.InformationHeaderLabel.Location = new System.Drawing.Point(11, 21);
            this.InformationHeaderLabel.Name = "InformationHeaderLabel";
            this.InformationHeaderLabel.Size = new System.Drawing.Size(336, 31);
            this.InformationHeaderLabel.TabIndex = 0;
            this.InformationHeaderLabel.Text = "[AppName] has encountered a problem and needs to close.  We are sorry for the inc" +
    "onvenience.";
            // 
            // InformationBodyLabel
            // 
            this.InformationBodyLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.InformationBodyLabel.Location = new System.Drawing.Point(9, 71);
            this.InformationBodyLabel.Name = "InformationBodyLabel";
            this.InformationBodyLabel.Size = new System.Drawing.Size(375, 131);
            this.InformationBodyLabel.TabIndex = 1;
            this.InformationBodyLabel.Text = resources.GetString("InformationBodyLabel.Text");
            // 
            // buttonGroupBox
            // 
            this.buttonGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonGroupBox.Controls.Add(this.ExitButton);
            this.buttonGroupBox.Controls.Add(this.SendReportButton);
            this.buttonGroupBox.Location = new System.Drawing.Point(-3, 395);
            this.buttonGroupBox.Name = "buttonGroupBox";
            this.buttonGroupBox.Size = new System.Drawing.Size(401, 54);
            this.buttonGroupBox.TabIndex = 8;
            this.buttonGroupBox.TabStop = false;
            // 
            // ExitButton
            // 
            this.ExitButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ExitButton.CausesValidation = false;
            this.ExitButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.ExitButton.Location = new System.Drawing.Point(297, 19);
            this.ExitButton.Name = "ExitButton";
            this.ExitButton.Size = new System.Drawing.Size(90, 23);
            this.ExitButton.TabIndex = 1;
            this.ExitButton.Text = "&Cancel";
            this.ExitButton.Click += new System.EventHandler(this.ExitButton_Click);
            // 
            // SendReportButton
            // 
            this.SendReportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SendReportButton.Location = new System.Drawing.Point(201, 19);
            this.SendReportButton.Name = "SendReportButton";
            this.SendReportButton.Size = new System.Drawing.Size(90, 23);
            this.SendReportButton.TabIndex = 0;
            this.SendReportButton.Text = "&Submit Report";
            this.SendReportButton.Click += new System.EventHandler(this.OnSubmitReportButtonClick);
            // 
            // CrashReportForm
            // 
            this.AcceptButton = this.SendReportButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.ExitButton;
            this.ClientSize = new System.Drawing.Size(396, 447);
            this.Controls.Add(this.buttonGroupBox);
            this.Controls.Add(this.InformationBodyLabel);
            this.Controls.Add(this.titleGroupBox);
            this.Controls.Add(this.DescriptionTextBox);
            this.Controls.Add(this.EmailAddressTextBox);
            this.Controls.Add(this.EmailAddressLabel);
            this.Controls.Add(this.DescriptionLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CrashReportForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "[AppName] Error";
            this.titleGroupBox.ResumeLayout(false);
            this.titleGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.InformationHeaderPictureBox)).EndInit();
            this.buttonGroupBox.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox DescriptionTextBox;
        private System.Windows.Forms.TextBox EmailAddressTextBox;
        private System.Windows.Forms.Label EmailAddressLabel;
        private System.Windows.Forms.Label DescriptionLabel;
        private System.Windows.Forms.GroupBox titleGroupBox;
        private System.Windows.Forms.Label InformationHeaderLabel;
        private System.Windows.Forms.PictureBox InformationHeaderPictureBox;
        private System.Windows.Forms.Label InformationBodyLabel;
        private System.Windows.Forms.GroupBox buttonGroupBox;
        private System.Windows.Forms.Button ExitButton;
        private System.Windows.Forms.Button SendReportButton;
    }
}

