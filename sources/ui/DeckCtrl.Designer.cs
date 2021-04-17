namespace FFTriadBuddy
{
    partial class DeckCtrl
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
            this.contextMenuStripPickCard = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuOnlyOwned = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripComboBoxPick = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripMenuLockOptimization = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripPickCard.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStripPickCard
            // 
            this.contextMenuStripPickCard.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem2,
            this.toolStripMenuOnlyOwned,
            this.toolStripMenuLockOptimization,
            this.toolStripComboBoxPick});
            this.contextMenuStripPickCard.Name = "contextMenuStripPickCard";
            this.contextMenuStripPickCard.Size = new System.Drawing.Size(311, 119);
            this.contextMenuStripPickCard.Closing += new System.Windows.Forms.ToolStripDropDownClosingEventHandler(this.contextMenuStripPickCard_Closing);
            this.contextMenuStripPickCard.Opened += new System.EventHandler(this.contextMenuStripPickCard_Opened);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Enabled = false;
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(310, 22);
            this.toolStripMenuItem2.Text = "Pick card:";
            // 
            // toolStripMenuOnlyOwned
            // 
            this.toolStripMenuOnlyOwned.CheckOnClick = true;
            this.toolStripMenuOnlyOwned.Name = "toolStripMenuOnlyOwned";
            this.toolStripMenuOnlyOwned.Size = new System.Drawing.Size(310, 22);
            this.toolStripMenuOnlyOwned.Text = "Use only owned cards (click to change)";
            this.toolStripMenuOnlyOwned.CheckedChanged += new System.EventHandler(this.toolStripMenuOnlyOwned_CheckedChanged);
            // 
            // toolStripComboBoxPick
            // 
            this.toolStripComboBoxPick.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.toolStripComboBoxPick.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.toolStripComboBoxPick.Name = "toolStripComboBoxPick";
            this.toolStripComboBoxPick.Size = new System.Drawing.Size(250, 23);
            this.toolStripComboBoxPick.Sorted = true;
            this.toolStripComboBoxPick.SelectedIndexChanged += new System.EventHandler(this.toolStripComboBoxPick_SelectedIndexChanged);
            // 
            // toolStripMenuLockOptimization
            // 
            this.toolStripMenuLockOptimization.CheckOnClick = true;
            this.toolStripMenuLockOptimization.Name = "toolStripMenuLockOptimization";
            this.toolStripMenuLockOptimization.Size = new System.Drawing.Size(310, 22);
            this.toolStripMenuLockOptimization.Text = "Lock card for deck optimization";
            this.toolStripMenuLockOptimization.CheckedChanged += new System.EventHandler(this.toolStripMenuLockOptimization_CheckedChanged);
            // 
            // DeckCtrl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "DeckCtrl";
            this.Size = new System.Drawing.Size(280, 51);
            this.contextMenuStripPickCard.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip contextMenuStripPickCard;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuOnlyOwned;
        private System.Windows.Forms.ToolStripComboBox toolStripComboBoxPick;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuLockOptimization;
    }
}
