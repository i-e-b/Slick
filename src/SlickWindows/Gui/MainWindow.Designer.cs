namespace SlickWindows.Gui
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.paletteButton = new System.Windows.Forms.Button();
            this.mapButton = new System.Windows.Forms.Button();
            this.setPageButton = new System.Windows.Forms.Button();
            this.CanvasToolTips = new System.Windows.Forms.ToolTip(this.components);
            this.moreButton = new System.Windows.Forms.Button();
            this.pickFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.SuspendLayout();
            // 
            // paletteButton
            // 
            this.paletteButton.AccessibleDescription = "Set the pen color and size for drawing";
            this.paletteButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.paletteButton.Location = new System.Drawing.Point(6, 6);
            this.paletteButton.Margin = new System.Windows.Forms.Padding(2);
            this.paletteButton.Name = "paletteButton";
            this.paletteButton.Size = new System.Drawing.Size(75, 34);
            this.paletteButton.TabIndex = 0;
            this.paletteButton.Text = "Palette";
            this.CanvasToolTips.SetToolTip(this.paletteButton, "Set the pen color and size for drawing");
            this.paletteButton.UseVisualStyleBackColor = true;
            this.paletteButton.Click += new System.EventHandler(this.paletteButton_Click);
            // 
            // mapButton
            // 
            this.mapButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.mapButton.Location = new System.Drawing.Point(6, 298);
            this.mapButton.Margin = new System.Windows.Forms.Padding(2);
            this.mapButton.Name = "mapButton";
            this.mapButton.Size = new System.Drawing.Size(75, 34);
            this.mapButton.TabIndex = 1;
            this.mapButton.Text = "Map";
            this.CanvasToolTips.SetToolTip(this.mapButton, "Display an overview of the current canvas");
            this.mapButton.UseVisualStyleBackColor = true;
            this.mapButton.Click += new System.EventHandler(this.mapButton_Click);
            // 
            // setPageButton
            // 
            this.setPageButton.AccessibleDescription = "Change the storage location used for the canvas";
            this.setPageButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.setPageButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.setPageButton.Location = new System.Drawing.Point(402, 294);
            this.setPageButton.Margin = new System.Windows.Forms.Padding(2);
            this.setPageButton.Name = "setPageButton";
            this.setPageButton.Size = new System.Drawing.Size(75, 34);
            this.setPageButton.TabIndex = 2;
            this.setPageButton.Text = "Set Page";
            this.CanvasToolTips.SetToolTip(this.setPageButton, "Change the storage location used for the canvas.\r\nThis acts like different \'pages" +
        "\'");
            this.setPageButton.UseVisualStyleBackColor = true;
            this.setPageButton.Click += new System.EventHandler(this.SetPageButton_Click);
            // 
            // CanvasToolTips
            // 
            this.CanvasToolTips.IsBalloon = true;
            // 
            // moreButton
            // 
            this.moreButton.AccessibleDescription = "Access less frequently needed settings and features";
            this.moreButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.moreButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.moreButton.Location = new System.Drawing.Point(402, 6);
            this.moreButton.Margin = new System.Windows.Forms.Padding(2);
            this.moreButton.Name = "moreButton";
            this.moreButton.Size = new System.Drawing.Size(75, 34);
            this.moreButton.TabIndex = 3;
            this.moreButton.Text = "More...";
            this.CanvasToolTips.SetToolTip(this.moreButton, "Access less frequently needed settings and features");
            this.moreButton.UseVisualStyleBackColor = true;
            this.moreButton.Click += new System.EventHandler(this.MoreButton_Click);
            // 
            // pickFolderDialog
            // 
            this.pickFolderDialog.Description = "Pick a storage location for this page. It should be an empty folder";
            this.pickFolderDialog.RootFolder = System.Environment.SpecialFolder.MyComputer;
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(488, 339);
            this.Controls.Add(this.moreButton);
            this.Controls.Add(this.setPageButton);
            this.Controls.Add(this.mapButton);
            this.Controls.Add(this.paletteButton);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "MainWindow";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "Slick";
            this.ClientSizeChanged += new System.EventHandler(this.MainWindow_ClientSizeChanged);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button paletteButton;
        private System.Windows.Forms.Button mapButton;
        private System.Windows.Forms.ToolTip CanvasToolTips;
        private System.Windows.Forms.Button setPageButton;
        private System.Windows.Forms.FolderBrowserDialog pickFolderDialog;
        private System.Windows.Forms.Button moreButton;
    }
}

