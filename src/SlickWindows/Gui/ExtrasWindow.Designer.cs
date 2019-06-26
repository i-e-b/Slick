namespace SlickWindows.Gui
{
    partial class Extras
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
            this.importButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.exportButton = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.saveJpegDialog = new System.Windows.Forms.SaveFileDialog();
            this.loadImageDialog = new System.Windows.Forms.OpenFileDialog();
            this.label3 = new System.Windows.Forms.Label();
            this.textInputButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // importButton
            // 
            this.importButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.importButton.Location = new System.Drawing.Point(320, 111);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(75, 42);
            this.importButton.TabIndex = 0;
            this.importButton.Text = "Import...";
            this.importButton.UseVisualStyleBackColor = true;
            this.importButton.Click += new System.EventHandler(this.ImportButton_Click);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(124, 114);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(190, 39);
            this.label1.TabIndex = 1;
            this.label1.Text = "Draw an external image onto the page.\r\nMerges existing tiles.\r\nThis will modify t" +
    "he current page.";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Location = new System.Drawing.Point(320, 172);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(75, 42);
            this.exportButton.TabIndex = 2;
            this.exportButton.Text = "Export...";
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.ExportButton_Click);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(51, 172);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(263, 39);
            this.label2.TabIndex = 3;
            this.label2.Text = "Export selected area to an image file.\r\nResult is rendered at full size regardles" +
    "s of \'map\' mode.\r\nDoes not modify the current page.";
            this.label2.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // saveJpegDialog
            // 
            this.saveJpegDialog.DefaultExt = "jpg";
            this.saveJpegDialog.FileName = "export.jpg";
            this.saveJpegDialog.Filter = "JPEG files|*.jpg|All Files|*.*";
            this.saveJpegDialog.RestoreDirectory = true;
            this.saveJpegDialog.Title = "Save selected region";
            // 
            // loadImageDialog
            // 
            this.loadImageDialog.FileName = "import.jpg";
            this.loadImageDialog.Filter = "All files|*.*";
            this.loadImageDialog.Title = "Pick an image to import";
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(193, 66);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(121, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Show a text input helper";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // textInputButton
            // 
            this.textInputButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.textInputButton.Location = new System.Drawing.Point(320, 51);
            this.textInputButton.Name = "textInputButton";
            this.textInputButton.Size = new System.Drawing.Size(75, 42);
            this.textInputButton.TabIndex = 4;
            this.textInputButton.Text = "Text Input";
            this.textInputButton.UseVisualStyleBackColor = true;
            this.textInputButton.Click += new System.EventHandler(this.TextInputButton_Click);
            // 
            // Extras
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(407, 226);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textInputButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.importButton);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Extras";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "ExtrasWindow";
            this.Shown += new System.EventHandler(this.Extras_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.SaveFileDialog saveJpegDialog;
        private System.Windows.Forms.OpenFileDialog loadImageDialog;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button textInputButton;
    }
}