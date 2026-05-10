namespace StaffGenerator.Forms
{
    partial class ExportProgressForm
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
            progressBar = new ProgressBar();
            labelProgress = new Label();
            labelCurrent = new Label();
            SuspendLayout();
            // 
            // progressBar
            // 
            progressBar.Location = new Point(12, 44);
            progressBar.MarqueeAnimationSpeed = 0;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(360, 23);
            progressBar.Step = 900;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.TabIndex = 0;
            progressBar.Value = 100;
            // 
            // labelProgress
            // 
            labelProgress.AutoSize = true;
            labelProgress.Location = new Point(12, 24);
            labelProgress.Name = "labelProgress";
            labelProgress.Size = new Size(45, 15);
            labelProgress.TabIndex = 1;
            labelProgress.Text = "0 / 0 件";
            // 
            // labelCurrent
            // 
            labelCurrent.Location = new Point(12, 75);
            labelCurrent.Name = "labelCurrent";
            labelCurrent.Size = new Size(360, 20);
            labelCurrent.TabIndex = 2;
            // 
            // ExportProgressForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(384, 107);
            Controls.Add(progressBar);
            Controls.Add(labelProgress);
            Controls.Add(labelCurrent);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ExportProgressForm";
            StartPosition = FormStartPosition.CenterParent;
            ResumeLayout(false);
            PerformLayout();
        }

        private ProgressBar progressBar;
        private Label labelProgress;
        private Label labelCurrent;

        #endregion
    }
}