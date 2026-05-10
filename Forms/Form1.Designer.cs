namespace StaffGenerator
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            button1 = new Button();
            pictureBox1 = new PictureBox();
            button2 = new Button();
            button3 = new Button();
            button4 = new Button();
            comboBox1 = new ComboBox();
            button5 = new Button();
            checkBox1 = new CheckBox();
            labelPage = new Label();
            button6 = new Button();
            button7 = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(12, 12);
            button1.Name = "button1";
            button1.Size = new Size(39, 23);
            button1.TabIndex = 0;
            button1.Text = "前へ";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.Location = new Point(12, 41);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(696, 948);
            pictureBox1.TabIndex = 1;
            pictureBox1.TabStop = false;
            // 
            // button2
            // 
            button2.Location = new Point(183, 13);
            button2.Name = "button2";
            button2.Size = new Size(64, 23);
            button2.TabIndex = 2;
            button2.Text = "json読込";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Location = new Point(57, 12);
            button3.Name = "button3";
            button3.Size = new Size(39, 23);
            button3.TabIndex = 3;
            button3.Text = "次へ";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.Location = new Point(345, 13);
            button4.Name = "button4";
            button4.Size = new Size(43, 23);
            button4.TabIndex = 4;
            button4.Text = "描画";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(253, 13);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(86, 23);
            comboBox1.TabIndex = 5;
            // 
            // button5
            // 
            button5.Location = new Point(394, 13);
            button5.Name = "button5";
            button5.Size = new Size(51, 23);
            button5.TabIndex = 6;
            button5.Text = "再描画";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(636, 15);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(84, 19);
            checkBox1.TabIndex = 7;
            checkBox1.Text = "さいぜんめん";
            checkBox1.UseVisualStyleBackColor = true;
            checkBox1.CheckedChanged += checkBox1_CheckedChanged;
            // 
            // labelPage
            // 
            labelPage.AutoSize = true;
            labelPage.Location = new Point(102, 16);
            labelPage.Name = "labelPage";
            labelPage.Size = new Size(38, 15);
            labelPage.TabIndex = 8;
            labelPage.Text = "label1";
            // 
            // button6
            // 
            button6.Location = new Point(496, 12);
            button6.Name = "button6";
            button6.Size = new Size(64, 23);
            button6.TabIndex = 9;
            button6.Text = "指定描画";
            button6.UseVisualStyleBackColor = true;
            button6.Click += button6_Click;
            // 
            // button7
            // 
            button7.Location = new Point(566, 12);
            button7.Name = "button7";
            button7.Size = new Size(64, 23);
            button7.TabIndex = 10;
            button7.Text = "全体描画";
            button7.UseVisualStyleBackColor = true;
            button7.Click += button7_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(722, 1005);
            Controls.Add(button7);
            Controls.Add(button6);
            Controls.Add(labelPage);
            Controls.Add(checkBox1);
            Controls.Add(button5);
            Controls.Add(comboBox1);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(pictureBox1);
            Controls.Add(button1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private PictureBox pictureBox1;
        private Button button2;
        private Button button3;
        private Button button4;
        private ComboBox comboBox1;
        private Button button5;
        private CheckBox checkBox1;
        private Label labelPage;
        private Button button6;
        private Button button7;
    }
}
