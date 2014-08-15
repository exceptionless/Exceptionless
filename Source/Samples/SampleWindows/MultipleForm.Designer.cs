namespace Exceptionless.SampleWindows
{
    partial class MultipleForm
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
            this.numberLabel = new System.Windows.Forms.Label();
            this.NumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.okButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.NumericUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // numberLabel
            // 
            this.numberLabel.AutoSize = true;
            this.numberLabel.Location = new System.Drawing.Point(13, 9);
            this.numberLabel.Name = "numberLabel";
            this.numberLabel.Size = new System.Drawing.Size(114, 13);
            this.numberLabel.TabIndex = 0;
            this.numberLabel.Text = "Number of Exceptions:";
            // 
            // NumericUpDown
            // 
            this.NumericUpDown.Location = new System.Drawing.Point(16, 29);
            this.NumericUpDown.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.NumericUpDown.Name = "NumericUpDown";
            this.NumericUpDown.Size = new System.Drawing.Size(214, 20);
            this.NumericUpDown.TabIndex = 1;
            this.NumericUpDown.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // okButton
            // 
            this.okButton.Location = new System.Drawing.Point(155, 70);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 2;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // MultipleForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(242, 105);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.NumericUpDown);
            this.Controls.Add(this.numberLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MultipleForm";
            this.Text = "Multiple Excpetions";
            ((System.ComponentModel.ISupportInitialize)(this.NumericUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label numberLabel;
        public System.Windows.Forms.NumericUpDown NumericUpDown;
        private System.Windows.Forms.Button okButton;
    }
}