using SlickWindows.Gui.Components;

namespace SlickWindows.Gui
{
    partial class FloatingImage
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
            this.removeButton = new SlickWindows.Gui.Components.RoundSymbolButton();
            this.mergeButton = new SlickWindows.Gui.Components.RoundSymbolButton();
            this.toolTips = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // removeButton
            // 
            this.removeButton.AccessibleDescription = "Remove the floating image without writing it to the page";
            this.removeButton.AccessibleName = "Remove";
            this.removeButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.removeButton.Location = new System.Drawing.Point(0, 0);
            this.removeButton.Name = "removeButton";
            this.removeButton.Size = new System.Drawing.Size(24, 24);
            this.removeButton.Symbol = SymbolType.Cross;
            this.removeButton.TabIndex = 0;
            this.toolTips.SetToolTip(this.removeButton, "Remove the floating image without writing it to the page");
            this.removeButton.Click += new System.EventHandler(this.RemoveButton_Click);
            // 
            // mergeButton
            // 
            this.mergeButton.AccessibleDescription = "Write the image to the page at the current position";
            this.mergeButton.AccessibleName = "Merge";
            this.mergeButton.AccessibleRole = System.Windows.Forms.AccessibleRole.PushButton;
            this.mergeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.mergeButton.Location = new System.Drawing.Point(126, 0);
            this.mergeButton.Name = "mergeButton";
            this.mergeButton.Size = new System.Drawing.Size(24, 24);
            this.mergeButton.Symbol = SymbolType.MergeArrow;
            this.mergeButton.TabIndex = 1;
            this.toolTips.SetToolTip(this.mergeButton, "Write the image to the page at the current position");
            this.mergeButton.Click += new System.EventHandler(this.MergeButton_Click);
            // 
            // toolTips
            // 
            this.toolTips.IsBalloon = true;
            // 
            // FloatingImage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.mergeButton);
            this.Controls.Add(this.removeButton);
            this.Name = "FloatingImage";
            this.ResumeLayout(false);

        }

        #endregion

        private SlickWindows.Gui.Components.RoundSymbolButton removeButton;
        private SlickWindows.Gui.Components.RoundSymbolButton mergeButton;
        private System.Windows.Forms.ToolTip toolTips;
    }
}
