namespace SlickWindows.Gui.Components
{
    partial class FloatingText
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.mergeButton = new SlickWindows.Gui.Components.RoundSymbolButton();
            this.closeButton = new SlickWindows.Gui.Components.RoundSymbolButton();
            this.textBox = new System.Windows.Forms.TextBox();
            this.textBiggerButton = new System.Windows.Forms.Button();
            this.textSmallerButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // toolTip1
            // 
            this.toolTip1.IsBalloon = true;
            // 
            // mergeButton
            // 
            this.mergeButton.AccessibleDescription = "Draw text onto the canvas";
            this.mergeButton.AccessibleName = "Merge";
            this.mergeButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.mergeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.mergeButton.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.mergeButton.Location = new System.Drawing.Point(229, 0);
            this.mergeButton.Name = "mergeButton";
            this.mergeButton.Size = new System.Drawing.Size(24, 24);
            this.mergeButton.Symbol = SlickWindows.Gui.Components.SymbolType.MergeArrow;
            this.mergeButton.TabIndex = 1;
            this.toolTip1.SetToolTip(this.mergeButton, "Draw text onto the canvas");
            this.mergeButton.Click += new System.EventHandler(this.MergeButton_Click);
            // 
            // closeButton
            // 
            this.closeButton.AccessibleDescription = "Close the text input without changing the page";
            this.closeButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.closeButton.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.closeButton.Location = new System.Drawing.Point(0, 0);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(24, 24);
            this.closeButton.Symbol = SlickWindows.Gui.Components.SymbolType.Cross;
            this.closeButton.TabIndex = 0;
            this.toolTip1.SetToolTip(this.closeButton, "Close the text input without changing the page");
            this.closeButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // textBox
            // 
            this.textBox.AcceptsReturn = true;
            this.textBox.AcceptsTab = true;
            this.textBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox.Location = new System.Drawing.Point(0, 22);
            this.textBox.Multiline = true;
            this.textBox.Name = "textBox";
            this.textBox.Size = new System.Drawing.Size(253, 158);
            this.textBox.TabIndex = 2;
            // 
            // textBiggerButton
            // 
            this.textBiggerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBiggerButton.Location = new System.Drawing.Point(0, 180);
            this.textBiggerButton.Name = "textBiggerButton";
            this.textBiggerButton.Size = new System.Drawing.Size(75, 23);
            this.textBiggerButton.TabIndex = 3;
            this.textBiggerButton.Text = "Large";
            this.textBiggerButton.UseVisualStyleBackColor = true;
            this.textBiggerButton.Click += new System.EventHandler(this.TextBiggerButton_Click);
            // 
            // textSmallerButton
            // 
            this.textSmallerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textSmallerButton.Location = new System.Drawing.Point(81, 180);
            this.textSmallerButton.Name = "textSmallerButton";
            this.textSmallerButton.Size = new System.Drawing.Size(75, 23);
            this.textSmallerButton.TabIndex = 4;
            this.textSmallerButton.Text = "Small";
            this.textSmallerButton.UseVisualStyleBackColor = true;
            this.textSmallerButton.Click += new System.EventHandler(this.TextSmallerButton_Click);
            // 
            // FloatingText
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.textSmallerButton);
            this.Controls.Add(this.textBiggerButton);
            this.Controls.Add(this.mergeButton);
            this.Controls.Add(this.closeButton);
            this.Controls.Add(this.textBox);
            this.DoubleBuffered = true;
            this.Name = "FloatingText";
            this.Size = new System.Drawing.Size(253, 203);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private RoundSymbolButton closeButton;
        private System.Windows.Forms.ToolTip toolTip1;
        private RoundSymbolButton mergeButton;
        private System.Windows.Forms.TextBox textBox;
        private System.Windows.Forms.Button textBiggerButton;
        private System.Windows.Forms.Button textSmallerButton;
    }
}
