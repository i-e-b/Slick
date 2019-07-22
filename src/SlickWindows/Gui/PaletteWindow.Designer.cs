namespace SlickWindows.Gui
{
    partial class PaletteWindow
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
            this.label1 = new System.Windows.Forms.Label();
            this.colorBox = new System.Windows.Forms.PictureBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.smallPenButton = new System.Windows.Forms.Button();
            this.medButton = new System.Windows.Forms.Button();
            this.largeButton = new System.Windows.Forms.Button();
            this.hugeButton = new System.Windows.Forms.Button();
            this.giganticButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.colorBox)).BeginInit();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(372, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Pick size first. Set input colour by using the input device on the colour palette" +
    ".";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // colorBox
            // 
            this.colorBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.colorBox.Location = new System.Drawing.Point(0, 13);
            this.colorBox.Name = "colorBox";
            this.colorBox.Size = new System.Drawing.Size(534, 298);
            this.colorBox.TabIndex = 1;
            this.colorBox.TabStop = false;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.smallPenButton);
            this.flowLayoutPanel1.Controls.Add(this.medButton);
            this.flowLayoutPanel1.Controls.Add(this.largeButton);
            this.flowLayoutPanel1.Controls.Add(this.hugeButton);
            this.flowLayoutPanel1.Controls.Add(this.giganticButton);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 271);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(534, 40);
            this.flowLayoutPanel1.TabIndex = 2;
            // 
            // smallPenButton
            // 
            this.smallPenButton.Location = new System.Drawing.Point(3, 3);
            this.smallPenButton.Name = "smallPenButton";
            this.smallPenButton.Size = new System.Drawing.Size(75, 23);
            this.smallPenButton.TabIndex = 0;
            this.smallPenButton.Text = "Small";
            this.smallPenButton.UseVisualStyleBackColor = true;
            this.smallPenButton.Click += new System.EventHandler(this.smallPenButton_Click);
            // 
            // medButton
            // 
            this.medButton.Location = new System.Drawing.Point(84, 3);
            this.medButton.Name = "medButton";
            this.medButton.Size = new System.Drawing.Size(75, 23);
            this.medButton.TabIndex = 1;
            this.medButton.Text = "Medium";
            this.medButton.UseVisualStyleBackColor = true;
            this.medButton.Click += new System.EventHandler(this.medButton_Click);
            // 
            // largeButton
            // 
            this.largeButton.Location = new System.Drawing.Point(165, 3);
            this.largeButton.Name = "largeButton";
            this.largeButton.Size = new System.Drawing.Size(75, 23);
            this.largeButton.TabIndex = 2;
            this.largeButton.Text = "Large";
            this.largeButton.UseVisualStyleBackColor = true;
            this.largeButton.Click += new System.EventHandler(this.largeButton_Click);
            // 
            // hugeButton
            // 
            this.hugeButton.Location = new System.Drawing.Point(246, 3);
            this.hugeButton.Name = "hugeButton";
            this.hugeButton.Size = new System.Drawing.Size(75, 23);
            this.hugeButton.TabIndex = 3;
            this.hugeButton.Text = "Huge";
            this.hugeButton.UseVisualStyleBackColor = true;
            this.hugeButton.Click += new System.EventHandler(this.hugeButton_Click);
            // 
            // giganticButton
            // 
            this.giganticButton.Location = new System.Drawing.Point(327, 3);
            this.giganticButton.Name = "giganticButton";
            this.giganticButton.Size = new System.Drawing.Size(75, 23);
            this.giganticButton.TabIndex = 4;
            this.giganticButton.Text = "Gigantic";
            this.giganticButton.UseVisualStyleBackColor = true;
            this.giganticButton.Click += new System.EventHandler(this.giganticButton_Click);
            // 
            // PaletteWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(534, 311);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.colorBox);
            this.Controls.Add(this.label1);
            this.DoubleBuffered = true;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(550, 350);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(300, 200);
            this.Name = "PaletteWindow";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Slick Palette";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PaletteWindow_FormClosing);
            this.DpiChanged += new System.Windows.Forms.DpiChangedEventHandler(this.PaletteWindow_DpiChanged);
            this.SizeChanged += new System.EventHandler(this.PaletteWindow_SizeChanged);
            ((System.ComponentModel.ISupportInitialize)(this.colorBox)).EndInit();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.PictureBox colorBox;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button smallPenButton;
        private System.Windows.Forms.Button medButton;
        private System.Windows.Forms.Button largeButton;
        private System.Windows.Forms.Button hugeButton;
        private System.Windows.Forms.Button giganticButton;
    }
}