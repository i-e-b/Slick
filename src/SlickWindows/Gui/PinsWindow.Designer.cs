namespace SlickWindows.Gui
{
    partial class PinsWindow
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
            this.pinListView = new System.Windows.Forms.ListView();
            this.addButton = new System.Windows.Forms.Button();
            this.viewButton = new System.Windows.Forms.Button();
            this.pinsToolTips = new System.Windows.Forms.ToolTip(this.components);
            this.newPinBox = new System.Windows.Forms.TextBox();
            this.deleteButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // pinListView
            // 
            this.pinListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pinListView.FullRowSelect = true;
            this.pinListView.Location = new System.Drawing.Point(12, 12);
            this.pinListView.MultiSelect = false;
            this.pinListView.Name = "pinListView";
            this.pinListView.Size = new System.Drawing.Size(275, 196);
            this.pinListView.TabIndex = 0;
            this.pinListView.UseCompatibleStateImageBehavior = false;
            this.pinListView.View = System.Windows.Forms.View.Tile;
            this.pinListView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.PinListView_ItemSelectionChanged);
            this.pinListView.DoubleClick += new System.EventHandler(this.PinListView_DoubleClick);
            // 
            // addButton
            // 
            this.addButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.addButton.Enabled = false;
            this.addButton.Location = new System.Drawing.Point(12, 240);
            this.addButton.Name = "addButton";
            this.addButton.Size = new System.Drawing.Size(75, 37);
            this.addButton.TabIndex = 1;
            this.addButton.Text = "Add Pin";
            this.pinsToolTips.SetToolTip(this.addButton, "Add a new pin at the centre of the current view");
            this.addButton.UseVisualStyleBackColor = true;
            this.addButton.Click += new System.EventHandler(this.AddButton_Click);
            // 
            // viewButton
            // 
            this.viewButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.viewButton.Enabled = false;
            this.viewButton.Location = new System.Drawing.Point(212, 240);
            this.viewButton.Name = "viewButton";
            this.viewButton.Size = new System.Drawing.Size(75, 37);
            this.viewButton.TabIndex = 2;
            this.viewButton.Text = "View";
            this.pinsToolTips.SetToolTip(this.viewButton, "Centre the main canvas on the selected pin");
            this.viewButton.UseVisualStyleBackColor = true;
            this.viewButton.Click += new System.EventHandler(this.ViewButton_Click);
            // 
            // pinsToolTips
            // 
            this.pinsToolTips.IsBalloon = true;
            // 
            // newPinBox
            // 
            this.newPinBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.newPinBox.Location = new System.Drawing.Point(13, 214);
            this.newPinBox.Name = "newPinBox";
            this.newPinBox.Size = new System.Drawing.Size(274, 20);
            this.newPinBox.TabIndex = 3;
            this.pinsToolTips.SetToolTip(this.newPinBox, "Name of the new pin (required)");
            this.newPinBox.TextChanged += new System.EventHandler(this.NewPinBox_TextChanged);
            // 
            // deleteButton
            // 
            this.deleteButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.deleteButton.Enabled = false;
            this.deleteButton.Location = new System.Drawing.Point(93, 252);
            this.deleteButton.Name = "deleteButton";
            this.deleteButton.Size = new System.Drawing.Size(75, 25);
            this.deleteButton.TabIndex = 4;
            this.deleteButton.Text = "Delete";
            this.pinsToolTips.SetToolTip(this.deleteButton, "Delete the selected pin");
            this.deleteButton.UseVisualStyleBackColor = true;
            this.deleteButton.Click += new System.EventHandler(this.DeleteButton_Click);
            // 
            // PinsWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(299, 289);
            this.Controls.Add(this.deleteButton);
            this.Controls.Add(this.newPinBox);
            this.Controls.Add(this.viewButton);
            this.Controls.Add(this.addButton);
            this.Controls.Add(this.pinListView);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PinsWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Pins";
            this.Shown += new System.EventHandler(this.PinsWindow_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView pinListView;
        private System.Windows.Forms.Button addButton;
        private System.Windows.Forms.Button viewButton;
        private System.Windows.Forms.ToolTip pinsToolTips;
        private System.Windows.Forms.TextBox newPinBox;
        private System.Windows.Forms.Button deleteButton;
    }
}