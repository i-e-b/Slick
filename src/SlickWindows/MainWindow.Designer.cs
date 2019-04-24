namespace SlickWindows
{
    partial class MainWindow
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
            this.paletteButton = new System.Windows.Forms.Button();
            this.mapButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // paletteButton
            // 
            this.paletteButton.Location = new System.Drawing.Point(12, 12);
            this.paletteButton.Name = "paletteButton";
            this.paletteButton.Size = new System.Drawing.Size(150, 66);
            this.paletteButton.TabIndex = 0;
            this.paletteButton.Text = "Palette";
            this.paletteButton.UseVisualStyleBackColor = true;
            this.paletteButton.Click += new System.EventHandler(this.paletteButton_Click);
            // 
            // mapButton
            // 
            this.mapButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.mapButton.Location = new System.Drawing.Point(12, 573);
            this.mapButton.Name = "mapButton";
            this.mapButton.Size = new System.Drawing.Size(150, 66);
            this.mapButton.TabIndex = 1;
            this.mapButton.Text = "Map";
            this.mapButton.UseVisualStyleBackColor = true;
            this.mapButton.Click += new System.EventHandler(this.mapButton_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(975, 651);
            this.Controls.Add(this.mapButton);
            this.Controls.Add(this.paletteButton);
            this.DoubleBuffered = true;
            this.Name = "MainWindow";
            this.Text = "Slick";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button paletteButton;
        private System.Windows.Forms.Button mapButton;
    }
}

