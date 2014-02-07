namespace Tester
{
    partial class FilterForm
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
            this.acceptedButton = new System.Windows.Forms.Button();
            this.ignoredButton = new System.Windows.Forms.Button();
            this.configButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // acceptedButton
            // 
            this.acceptedButton.Location = new System.Drawing.Point(41, 172);
            this.acceptedButton.Name = "acceptedButton";
            this.acceptedButton.Size = new System.Drawing.Size(191, 23);
            this.acceptedButton.TabIndex = 5;
            this.acceptedButton.Text = "Accepted Error";
            this.acceptedButton.UseVisualStyleBackColor = true;
            this.acceptedButton.Click += new System.EventHandler(this.acceptedButton_Click);
            // 
            // ignoredButton
            // 
            this.ignoredButton.Location = new System.Drawing.Point(41, 121);
            this.ignoredButton.Name = "ignoredButton";
            this.ignoredButton.Size = new System.Drawing.Size(191, 23);
            this.ignoredButton.TabIndex = 4;
            this.ignoredButton.Text = "Ignored Error";
            this.ignoredButton.UseVisualStyleBackColor = true;
            this.ignoredButton.Click += new System.EventHandler(this.ignoredButton_Click);
            // 
            // configButton
            // 
            this.configButton.Location = new System.Drawing.Point(41, 51);
            this.configButton.Name = "configButton";
            this.configButton.Size = new System.Drawing.Size(191, 23);
            this.configButton.TabIndex = 3;
            this.configButton.Text = "Load Configuration";
            this.configButton.UseVisualStyleBackColor = true;
            this.configButton.Click += new System.EventHandler(this.configButton_Click);
            // 
            // FilterForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.Controls.Add(this.acceptedButton);
            this.Controls.Add(this.ignoredButton);
            this.Controls.Add(this.configButton);
            this.Name = "FilterForm";
            this.Text = "FilterForm";
            this.Load += new System.EventHandler(this.FilterForm_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button acceptedButton;
        private System.Windows.Forms.Button ignoredButton;
        private System.Windows.Forms.Button configButton;
    }
}