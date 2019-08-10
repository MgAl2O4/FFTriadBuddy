namespace FFTriadBuddy
{
    partial class FormOverlay
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
            this.button1 = new System.Windows.Forms.Button();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.panelMarkerBoard = new FFTriadBuddy.HitInvisiblePanel();
            this.panel3 = new FFTriadBuddy.HitInvisiblePanel();
            this.panelMarkerDeck = new FFTriadBuddy.HitInvisiblePanel();
            this.panel1 = new FFTriadBuddy.HitInvisiblePanel();
            this.panelSummary = new FFTriadBuddy.HitInvisiblePanel();
            this.labelNpc = new FFTriadBuddy.HitInvisibleLabel();
            this.panelBoard = new FFTriadBuddy.HitInvisiblePanel();
            this.cardCtrl9 = new FFTriadBuddy.CardCtrl();
            this.cardCtrl8 = new FFTriadBuddy.CardCtrl();
            this.cardCtrl7 = new FFTriadBuddy.CardCtrl();
            this.cardCtrl6 = new FFTriadBuddy.CardCtrl();
            this.cardCtrl5 = new FFTriadBuddy.CardCtrl();
            this.cardCtrl4 = new FFTriadBuddy.CardCtrl();
            this.cardCtrl3 = new FFTriadBuddy.CardCtrl();
            this.cardCtrl2 = new FFTriadBuddy.CardCtrl();
            this.cardCtrl1 = new FFTriadBuddy.CardCtrl();
            this.deckCtrlRed = new FFTriadBuddy.DeckCtrl();
            this.labelRules = new FFTriadBuddy.HitInvisibleLabel();
            this.deckCtrlBlue = new FFTriadBuddy.DeckCtrl();
            this.panelMarkerBoard.SuspendLayout();
            this.panelMarkerDeck.SuspendLayout();
            this.panelSummary.SuspendLayout();
            this.panelBoard.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(175, 60);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(108, 56);
            this.button1.TabIndex = 8;
            this.button1.Text = "Capture";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // timer1
            // 
            this.timer1.Interval = 4000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // panelMarkerBoard
            // 
            this.panelMarkerBoard.BackColor = System.Drawing.Color.White;
            this.panelMarkerBoard.Controls.Add(this.panel3);
            this.panelMarkerBoard.Location = new System.Drawing.Point(556, 12);
            this.panelMarkerBoard.Name = "panelMarkerBoard";
            this.panelMarkerBoard.Padding = new System.Windows.Forms.Padding(5);
            this.panelMarkerBoard.Size = new System.Drawing.Size(60, 174);
            this.panelMarkerBoard.TabIndex = 8;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.Color.Fuchsia;
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(5, 5);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(50, 164);
            this.panel3.TabIndex = 0;
            // 
            // panelMarkerDeck
            // 
            this.panelMarkerDeck.BackColor = System.Drawing.Color.Turquoise;
            this.panelMarkerDeck.Controls.Add(this.panel1);
            this.panelMarkerDeck.Location = new System.Drawing.Point(492, 12);
            this.panelMarkerDeck.Name = "panelMarkerDeck";
            this.panelMarkerDeck.Padding = new System.Windows.Forms.Padding(5);
            this.panelMarkerDeck.Size = new System.Drawing.Size(58, 174);
            this.panelMarkerDeck.TabIndex = 7;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.Fuchsia;
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(5, 5);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(48, 164);
            this.panel1.TabIndex = 0;
            // 
            // panelSummary
            // 
            this.panelSummary.Controls.Add(this.button1);
            this.panelSummary.Controls.Add(this.labelNpc);
            this.panelSummary.Controls.Add(this.panelBoard);
            this.panelSummary.Controls.Add(this.deckCtrlRed);
            this.panelSummary.Controls.Add(this.labelRules);
            this.panelSummary.Controls.Add(this.deckCtrlBlue);
            this.panelSummary.Location = new System.Drawing.Point(12, 12);
            this.panelSummary.Name = "panelSummary";
            this.panelSummary.Size = new System.Drawing.Size(464, 174);
            this.panelSummary.TabIndex = 6;
            // 
            // labelNpc
            // 
            this.labelNpc.AutoSize = true;
            this.labelNpc.BackColor = System.Drawing.Color.White;
            this.labelNpc.Location = new System.Drawing.Point(3, 103);
            this.labelNpc.Name = "labelNpc";
            this.labelNpc.Size = new System.Drawing.Size(79, 13);
            this.labelNpc.TabIndex = 7;
            this.labelNpc.Text = "NPC: unknown";
            // 
            // panelBoard
            // 
            this.panelBoard.BackColor = System.Drawing.Color.AntiqueWhite;
            this.panelBoard.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelBoard.Controls.Add(this.cardCtrl9);
            this.panelBoard.Controls.Add(this.cardCtrl8);
            this.panelBoard.Controls.Add(this.cardCtrl7);
            this.panelBoard.Controls.Add(this.cardCtrl6);
            this.panelBoard.Controls.Add(this.cardCtrl5);
            this.panelBoard.Controls.Add(this.cardCtrl4);
            this.panelBoard.Controls.Add(this.cardCtrl3);
            this.panelBoard.Controls.Add(this.cardCtrl2);
            this.panelBoard.Controls.Add(this.cardCtrl1);
            this.panelBoard.Location = new System.Drawing.Point(289, 0);
            this.panelBoard.Name = "panelBoard";
            this.panelBoard.Size = new System.Drawing.Size(170, 170);
            this.panelBoard.TabIndex = 3;
            // 
            // cardCtrl9
            // 
            this.cardCtrl9.AllowDrop = true;
            this.cardCtrl9.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl9.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl9.Location = new System.Drawing.Point(115, 115);
            this.cardCtrl9.Name = "cardCtrl9";
            this.cardCtrl9.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl9.TabIndex = 8;
            // 
            // cardCtrl8
            // 
            this.cardCtrl8.AllowDrop = true;
            this.cardCtrl8.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl8.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl8.Location = new System.Drawing.Point(59, 115);
            this.cardCtrl8.Name = "cardCtrl8";
            this.cardCtrl8.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl8.TabIndex = 7;
            // 
            // cardCtrl7
            // 
            this.cardCtrl7.AllowDrop = true;
            this.cardCtrl7.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl7.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl7.Location = new System.Drawing.Point(3, 115);
            this.cardCtrl7.Name = "cardCtrl7";
            this.cardCtrl7.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl7.TabIndex = 6;
            // 
            // cardCtrl6
            // 
            this.cardCtrl6.AllowDrop = true;
            this.cardCtrl6.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl6.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl6.Location = new System.Drawing.Point(115, 59);
            this.cardCtrl6.Name = "cardCtrl6";
            this.cardCtrl6.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl6.TabIndex = 5;
            // 
            // cardCtrl5
            // 
            this.cardCtrl5.AllowDrop = true;
            this.cardCtrl5.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl5.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl5.Location = new System.Drawing.Point(59, 59);
            this.cardCtrl5.Name = "cardCtrl5";
            this.cardCtrl5.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl5.TabIndex = 4;
            // 
            // cardCtrl4
            // 
            this.cardCtrl4.AllowDrop = true;
            this.cardCtrl4.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl4.Location = new System.Drawing.Point(3, 59);
            this.cardCtrl4.Name = "cardCtrl4";
            this.cardCtrl4.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl4.TabIndex = 3;
            // 
            // cardCtrl3
            // 
            this.cardCtrl3.AllowDrop = true;
            this.cardCtrl3.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl3.Location = new System.Drawing.Point(115, 3);
            this.cardCtrl3.Name = "cardCtrl3";
            this.cardCtrl3.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl3.TabIndex = 2;
            // 
            // cardCtrl2
            // 
            this.cardCtrl2.AllowDrop = true;
            this.cardCtrl2.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl2.Location = new System.Drawing.Point(59, 3);
            this.cardCtrl2.Name = "cardCtrl2";
            this.cardCtrl2.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl2.TabIndex = 1;
            // 
            // cardCtrl1
            // 
            this.cardCtrl1.AllowDrop = true;
            this.cardCtrl1.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl1.Location = new System.Drawing.Point(3, 3);
            this.cardCtrl1.Name = "cardCtrl1";
            this.cardCtrl1.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl1.TabIndex = 0;
            // 
            // deckCtrlRed
            // 
            this.deckCtrlRed.BackColor = System.Drawing.Color.LightCoral;
            this.deckCtrlRed.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.deckCtrlRed.Location = new System.Drawing.Point(3, 119);
            this.deckCtrlRed.Name = "deckCtrlRed";
            this.deckCtrlRed.Size = new System.Drawing.Size(280, 51);
            this.deckCtrlRed.TabIndex = 1;
            // 
            // labelRules
            // 
            this.labelRules.AutoSize = true;
            this.labelRules.BackColor = System.Drawing.Color.White;
            this.labelRules.Location = new System.Drawing.Point(3, 60);
            this.labelRules.Name = "labelRules";
            this.labelRules.Size = new System.Drawing.Size(84, 13);
            this.labelRules.TabIndex = 5;
            this.labelRules.Text = "Rules: unknown";
            // 
            // deckCtrlBlue
            // 
            this.deckCtrlBlue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(150)))), ((int)(((byte)(166)))));
            this.deckCtrlBlue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.deckCtrlBlue.Location = new System.Drawing.Point(3, 4);
            this.deckCtrlBlue.Name = "deckCtrlBlue";
            this.deckCtrlBlue.Size = new System.Drawing.Size(280, 51);
            this.deckCtrlBlue.TabIndex = 0;
            // 
            // FormOverlay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Fuchsia;
            this.ClientSize = new System.Drawing.Size(816, 192);
            this.ControlBox = false;
            this.Controls.Add(this.panelMarkerBoard);
            this.Controls.Add(this.panelMarkerDeck);
            this.Controls.Add(this.panelSummary);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "FormOverlay";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "FormOverlay";
            this.TopMost = true;
            this.TransparencyKey = System.Drawing.Color.Fuchsia;
            this.panelMarkerBoard.ResumeLayout(false);
            this.panelMarkerDeck.ResumeLayout(false);
            this.panelSummary.ResumeLayout(false);
            this.panelSummary.PerformLayout();
            this.panelBoard.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private DeckCtrl deckCtrlBlue;
        private DeckCtrl deckCtrlRed;
        private CardCtrl cardCtrl9;
        private CardCtrl cardCtrl8;
        private CardCtrl cardCtrl7;
        private CardCtrl cardCtrl6;
        private CardCtrl cardCtrl5;
        private CardCtrl cardCtrl4;
        private CardCtrl cardCtrl3;
        private CardCtrl cardCtrl2;
        private CardCtrl cardCtrl1;
        private HitInvisiblePanel panelBoard;
        private HitInvisibleLabel labelRules;
        private HitInvisiblePanel panelSummary;
        private HitInvisibleLabel labelNpc;
        private HitInvisiblePanel panelMarkerDeck;
        private HitInvisiblePanel panel1;
        private HitInvisiblePanel panelMarkerBoard;
        private HitInvisiblePanel panel3;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Timer timer1;
    }
}