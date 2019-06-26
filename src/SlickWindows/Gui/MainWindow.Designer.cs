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
            this.undoButton = new System.Windows.Forms.Button();
            this.pinsButton = new System.Windows.Forms.Button();
            this.selectButton = new System.Windows.Forms.Button();
            this.pickFolderDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.floatingImage1 = new SlickWindows.Gui.FloatingImage();
            this.floatingText1 = new SlickWindows.Gui.Components.FloatingText();
            this.SuspendLayout();
            // 
            // paletteButton
            // 
            this.paletteButton.AccessibleDescription = "Set the pen color and size for drawing";
            this.paletteButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.paletteButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.paletteButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Silver;
            this.paletteButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.paletteButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
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
            this.mapButton.AccessibleDescription = "Display an overview of the current canvas";
            this.mapButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.mapButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.mapButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.mapButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Silver;
            this.mapButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.mapButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
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
            this.setPageButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.setPageButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Silver;
            this.setPageButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.setPageButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
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
            this.moreButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.moreButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.moreButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Silver;
            this.moreButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.moreButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.moreButton.Location = new System.Drawing.Point(402, 219);
            this.moreButton.Margin = new System.Windows.Forms.Padding(2);
            this.moreButton.Name = "moreButton";
            this.moreButton.Size = new System.Drawing.Size(75, 33);
            this.moreButton.TabIndex = 3;
            this.moreButton.Text = "More...";
            this.CanvasToolTips.SetToolTip(this.moreButton, "Access less frequently needed settings and features");
            this.moreButton.UseVisualStyleBackColor = true;
            this.moreButton.Click += new System.EventHandler(this.MoreButton_Click);
            // 
            // undoButton
            // 
            this.undoButton.AccessibleDescription = "Undo the last ink stroke";
            this.undoButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.undoButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.undoButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.undoButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Silver;
            this.undoButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.undoButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.undoButton.Location = new System.Drawing.Point(402, 11);
            this.undoButton.Margin = new System.Windows.Forms.Padding(2);
            this.undoButton.Name = "undoButton";
            this.undoButton.Size = new System.Drawing.Size(75, 34);
            this.undoButton.TabIndex = 4;
            this.undoButton.Text = "Undo";
            this.CanvasToolTips.SetToolTip(this.undoButton, "Undo the last ink stroke");
            this.undoButton.UseVisualStyleBackColor = true;
            this.undoButton.Click += new System.EventHandler(this.UndoButton_Click);
            // 
            // pinsButton
            // 
            this.pinsButton.AccessibleDescription = "Use pins to navigate larger pages";
            this.pinsButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.pinsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.pinsButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.pinsButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Silver;
            this.pinsButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.pinsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.pinsButton.Location = new System.Drawing.Point(6, 260);
            this.pinsButton.Margin = new System.Windows.Forms.Padding(2);
            this.pinsButton.Name = "pinsButton";
            this.pinsButton.Size = new System.Drawing.Size(75, 34);
            this.pinsButton.TabIndex = 5;
            this.pinsButton.Text = "Pins";
            this.CanvasToolTips.SetToolTip(this.pinsButton, "Use pins to navigate larger pages");
            this.pinsButton.UseVisualStyleBackColor = true;
            this.pinsButton.Click += new System.EventHandler(this.PinsButton_Click);
            // 
            // selectButton
            // 
            this.selectButton.AccessibleDescription = "Select regions of the canvas for export";
            this.selectButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.selectButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.selectButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.selectButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Silver;
            this.selectButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.selectButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.selectButton.Location = new System.Drawing.Point(402, 256);
            this.selectButton.Margin = new System.Windows.Forms.Padding(2);
            this.selectButton.Name = "selectButton";
            this.selectButton.Size = new System.Drawing.Size(75, 34);
            this.selectButton.TabIndex = 6;
            this.selectButton.Text = "Select";
            this.CanvasToolTips.SetToolTip(this.selectButton, "Select regions of the canvas for export");
            this.selectButton.UseVisualStyleBackColor = true;
            this.selectButton.Click += new System.EventHandler(this.SelectButton_Click);
            // 
            // pickFolderDialog
            // 
            this.pickFolderDialog.Description = "Pick a storage location for this page. It should be an empty folder";
            this.pickFolderDialog.RootFolder = System.Environment.SpecialFolder.MyComputer;
            // 
            // saveFileDialog
            // 
            this.saveFileDialog.DefaultExt = "slick";
            this.saveFileDialog.Filter = "Slick Files|*.slick";
            this.saveFileDialog.OverwritePrompt = false;
            this.saveFileDialog.Title = "Pick page file";
            // 
            // floatingImage1
            // 
            this.floatingImage1.CandidateImage = null;
            this.floatingImage1.CanvasTarget = null;
            this.floatingImage1.Location = new System.Drawing.Point(163, 73);
            this.floatingImage1.Name = "floatingImage1";
            this.floatingImage1.Size = new System.Drawing.Size(150, 150);
            this.floatingImage1.TabIndex = 7;
            this.floatingImage1.Visible = false;
            // 
            // floatingText1
            // 
            this.floatingText1.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.floatingText1.CanvasTarget = null;
            this.floatingText1.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.floatingText1.Location = new System.Drawing.Point(115, 47);
            this.floatingText1.Name = "floatingText1";
            this.floatingText1.Size = new System.Drawing.Size(255, 205);
            this.floatingText1.TabIndex = 8;
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(488, 339);
            this.Controls.Add(this.floatingImage1);
            this.Controls.Add(this.selectButton);
            this.Controls.Add(this.pinsButton);
            this.Controls.Add(this.undoButton);
            this.Controls.Add(this.moreButton);
            this.Controls.Add(this.setPageButton);
            this.Controls.Add(this.mapButton);
            this.Controls.Add(this.paletteButton);
            this.Controls.Add(this.floatingText1);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "MainWindow";
            this.Text = "Slick";
            this.ClientSizeChanged += new System.EventHandler(this.MainWindow_ClientSizeChanged);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MainWindow_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.MainWindow_KeyUp);
            this.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.MainWindow_MouseDoubleClick);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button paletteButton;
        private System.Windows.Forms.Button mapButton;
        private System.Windows.Forms.ToolTip CanvasToolTips;
        private System.Windows.Forms.Button setPageButton;
        private System.Windows.Forms.FolderBrowserDialog pickFolderDialog;
        private System.Windows.Forms.Button moreButton;
        private System.Windows.Forms.SaveFileDialog saveFileDialog;
        private System.Windows.Forms.Button undoButton;
        private System.Windows.Forms.Button pinsButton;
        private System.Windows.Forms.Button selectButton;
        private FloatingImage floatingImage1;
        private Components.FloatingText floatingText1;
    }
}

