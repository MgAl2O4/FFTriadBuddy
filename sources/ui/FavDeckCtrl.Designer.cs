namespace FFTriadBuddy
{
    partial class FavDeckCtrl
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
            this.buttonUse = new System.Windows.Forms.Button();
            this.buttonEdit = new System.Windows.Forms.Button();
            this.deckCtrl = new FFTriadBuddy.DeckCtrl();
            this.labelTitle = new System.Windows.Forms.Label();
            this.labelChance = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // buttonUse
            // 
            this.buttonUse.Location = new System.Drawing.Point(3, 3);
            this.buttonUse.Name = "buttonUse";
            this.buttonUse.Size = new System.Drawing.Size(32, 29);
            this.buttonUse.TabIndex = 11;
            this.buttonUse.Text = "<<";
            this.buttonUse.UseVisualStyleBackColor = true;
            this.buttonUse.Click += new System.EventHandler(this.buttonUse_Click);
            // 
            // buttonEdit
            // 
            this.buttonEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonEdit.Location = new System.Drawing.Point(266, 3);
            this.buttonEdit.Name = "buttonEdit";
            this.buttonEdit.Size = new System.Drawing.Size(51, 29);
            this.buttonEdit.TabIndex = 10;
            this.buttonEdit.Text = "Edit";
            this.buttonEdit.UseVisualStyleBackColor = true;
            this.buttonEdit.Click += new System.EventHandler(this.buttonEdit_Click);
            // 
            // deckCtrl
            // 
            this.deckCtrl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.deckCtrl.Location = new System.Drawing.Point(97, 3);
            this.deckCtrl.Name = "deckCtrl";
            this.deckCtrl.Size = new System.Drawing.Size(172, 29);
            this.deckCtrl.TabIndex = 8;
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Location = new System.Drawing.Point(41, 3);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(35, 13);
            this.labelTitle.TabIndex = 7;
            this.labelTitle.Text = "Fav #";
            // 
            // labelChance
            // 
            this.labelChance.AutoSize = true;
            this.labelChance.Location = new System.Drawing.Point(41, 19);
            this.labelChance.Name = "labelChance";
            this.labelChance.Size = new System.Drawing.Size(27, 13);
            this.labelChance.TabIndex = 9;
            this.labelChance.Text = "88%";
            // 
            // FavDeckCtrl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.buttonUse);
            this.Controls.Add(this.buttonEdit);
            this.Controls.Add(this.labelChance);
            this.Controls.Add(this.deckCtrl);
            this.Controls.Add(this.labelTitle);
            this.Name = "FavDeckCtrl";
            this.Size = new System.Drawing.Size(320, 34);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonUse;
        private System.Windows.Forms.Button buttonEdit;
        private DeckCtrl deckCtrl;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Label labelChance;
    }
}
