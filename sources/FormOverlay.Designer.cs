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
            this.timerFadeMarkers = new System.Windows.Forms.Timer(this.components);
            this.panelSummary = new System.Windows.Forms.Panel();
            this.checkBoxAutoScan = new System.Windows.Forms.CheckBox();
            this.hitInvisibleLabel1 = new FFTriadBuddy.HitInvisibleLabel();
            this.labelStatus = new FFTriadBuddy.HitInvisibleLabel();
            this.checkBoxDetails = new System.Windows.Forms.CheckBox();
            this.buttonCapture = new System.Windows.Forms.Button();
            this.panelDetails = new FFTriadBuddy.HitInvisiblePanel();
            this.deckCtrlBlue = new FFTriadBuddy.DeckCtrl();
            this.deckCtrlRed = new FFTriadBuddy.DeckCtrl();
            this.labelNpc = new FFTriadBuddy.HitInvisibleLabel();
            this.labelRules = new FFTriadBuddy.HitInvisibleLabel();
            this.panelMarkerBoard = new FFTriadBuddy.HitInvisiblePanel();
            this.panel3 = new FFTriadBuddy.HitInvisiblePanel();
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
            this.panelMarkerDeck = new FFTriadBuddy.HitInvisiblePanel();
            this.panel1 = new FFTriadBuddy.HitInvisiblePanel();
            this.timerTurnScan = new System.Windows.Forms.Timer(this.components);
            this.panelDebug = new FFTriadBuddy.HitInvisiblePanel();
            this.pictureDebugScreen = new System.Windows.Forms.PictureBox();
            this.labelDebugDesc = new FFTriadBuddy.HitInvisibleLabel();
            this.labelDebugTime = new FFTriadBuddy.HitInvisibleLabel();
            this.timerDashAnim = new System.Windows.Forms.Timer(this.components);
            this.panelSummary.SuspendLayout();
            this.panelDetails.SuspendLayout();
            this.panelMarkerBoard.SuspendLayout();
            this.panelBoard.SuspendLayout();
            this.panelMarkerDeck.SuspendLayout();
            this.panelDebug.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureDebugScreen)).BeginInit();
            this.SuspendLayout();
            // 
            // timerFadeMarkers
            // 
            this.timerFadeMarkers.Interval = 4000;
            this.timerFadeMarkers.Tick += new System.EventHandler(this.timerFadeMarkers_Tick);
            // 
            // panelSummary
            // 
            this.panelSummary.BackColor = System.Drawing.SystemColors.Control;
            this.panelSummary.Controls.Add(this.checkBoxAutoScan);
            this.panelSummary.Controls.Add(this.hitInvisibleLabel1);
            this.panelSummary.Controls.Add(this.labelStatus);
            this.panelSummary.Controls.Add(this.checkBoxDetails);
            this.panelSummary.Controls.Add(this.buttonCapture);
            this.panelSummary.Location = new System.Drawing.Point(12, 12);
            this.panelSummary.Name = "panelSummary";
            this.panelSummary.Size = new System.Drawing.Size(214, 103);
            this.panelSummary.TabIndex = 6;
            this.panelSummary.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelSummary_MouseDown);
            this.panelSummary.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelSummary_MouseMove);
            this.panelSummary.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelSummary_MouseUp);
            // 
            // checkBoxAutoScan
            // 
            this.checkBoxAutoScan.AutoSize = true;
            this.checkBoxAutoScan.Location = new System.Drawing.Point(135, 80);
            this.checkBoxAutoScan.Name = "checkBoxAutoScan";
            this.checkBoxAutoScan.Size = new System.Drawing.Size(74, 17);
            this.checkBoxAutoScan.TabIndex = 12;
            this.checkBoxAutoScan.Text = "Auto scan";
            this.checkBoxAutoScan.UseVisualStyleBackColor = true;
            this.checkBoxAutoScan.CheckedChanged += new System.EventHandler(this.checkBoxAutoScan_CheckedChanged);
            // 
            // hitInvisibleLabel1
            // 
            this.hitInvisibleLabel1.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.hitInvisibleLabel1.Location = new System.Drawing.Point(3, 38);
            this.hitInvisibleLabel1.Name = "hitInvisibleLabel1";
            this.hitInvisibleLabel1.Size = new System.Drawing.Size(39, 39);
            this.hitInvisibleLabel1.TabIndex = 11;
            this.hitInvisibleLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelStatus
            // 
            this.labelStatus.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelStatus.Location = new System.Drawing.Point(48, 38);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(161, 39);
            this.labelStatus.TabIndex = 10;
            this.labelStatus.Text = "Status";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // checkBoxDetails
            // 
            this.checkBoxDetails.AutoSize = true;
            this.checkBoxDetails.Location = new System.Drawing.Point(4, 80);
            this.checkBoxDetails.Name = "checkBoxDetails";
            this.checkBoxDetails.Size = new System.Drawing.Size(86, 17);
            this.checkBoxDetails.TabIndex = 9;
            this.checkBoxDetails.Text = "Show details";
            this.checkBoxDetails.UseVisualStyleBackColor = true;
            this.checkBoxDetails.CheckedChanged += new System.EventHandler(this.checkBoxDetails_CheckedChanged);
            // 
            // buttonCapture
            // 
            this.buttonCapture.Location = new System.Drawing.Point(3, 5);
            this.buttonCapture.Name = "buttonCapture";
            this.buttonCapture.Size = new System.Drawing.Size(206, 30);
            this.buttonCapture.TabIndex = 8;
            this.buttonCapture.Text = "Capture";
            this.buttonCapture.UseVisualStyleBackColor = true;
            this.buttonCapture.Click += new System.EventHandler(this.buttonCapture_Click);
            this.buttonCapture.Paint += new System.Windows.Forms.PaintEventHandler(this.buttonCapture_Paint);
            // 
            // panelDetails
            // 
            this.panelDetails.BackColor = System.Drawing.Color.AntiqueWhite;
            this.panelDetails.Controls.Add(this.deckCtrlBlue);
            this.panelDetails.Controls.Add(this.deckCtrlRed);
            this.panelDetails.Controls.Add(this.labelNpc);
            this.panelDetails.Controls.Add(this.labelRules);
            this.panelDetails.Location = new System.Drawing.Point(12, 131);
            this.panelDetails.Name = "panelDetails";
            this.panelDetails.Size = new System.Drawing.Size(286, 162);
            this.panelDetails.TabIndex = 9;
            // 
            // deckCtrlBlue
            // 
            this.deckCtrlBlue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(150)))), ((int)(((byte)(166)))));
            this.deckCtrlBlue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.deckCtrlBlue.Location = new System.Drawing.Point(3, 3);
            this.deckCtrlBlue.Name = "deckCtrlBlue";
            this.deckCtrlBlue.Size = new System.Drawing.Size(280, 51);
            this.deckCtrlBlue.TabIndex = 0;
            // 
            // deckCtrlRed
            // 
            this.deckCtrlRed.BackColor = System.Drawing.Color.LightCoral;
            this.deckCtrlRed.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.deckCtrlRed.Location = new System.Drawing.Point(3, 107);
            this.deckCtrlRed.Name = "deckCtrlRed";
            this.deckCtrlRed.Size = new System.Drawing.Size(280, 51);
            this.deckCtrlRed.TabIndex = 1;
            // 
            // labelNpc
            // 
            this.labelNpc.AutoSize = true;
            this.labelNpc.BackColor = System.Drawing.Color.Transparent;
            this.labelNpc.Location = new System.Drawing.Point(3, 57);
            this.labelNpc.Name = "labelNpc";
            this.labelNpc.Size = new System.Drawing.Size(79, 13);
            this.labelNpc.TabIndex = 7;
            this.labelNpc.Text = "NPC: unknown";
            // 
            // labelRules
            // 
            this.labelRules.AutoSize = true;
            this.labelRules.BackColor = System.Drawing.Color.Transparent;
            this.labelRules.Location = new System.Drawing.Point(3, 91);
            this.labelRules.Name = "labelRules";
            this.labelRules.Size = new System.Drawing.Size(84, 13);
            this.labelRules.TabIndex = 5;
            this.labelRules.Text = "Rules: unknown";
            // 
            // panelMarkerBoard
            // 
            this.panelMarkerBoard.BackColor = System.Drawing.Color.White;
            this.panelMarkerBoard.Controls.Add(this.panel3);
            this.panelMarkerBoard.Location = new System.Drawing.Point(749, 11);
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
            this.panelBoard.Location = new System.Drawing.Point(304, 131);
            this.panelBoard.Name = "panelBoard";
            this.panelBoard.Size = new System.Drawing.Size(162, 162);
            this.panelBoard.TabIndex = 3;
            // 
            // cardCtrl9
            // 
            this.cardCtrl9.AllowDrop = true;
            this.cardCtrl9.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl9.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl9.Location = new System.Drawing.Point(107, 107);
            this.cardCtrl9.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrl9.Name = "cardCtrl9";
            this.cardCtrl9.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl9.TabIndex = 8;
            // 
            // cardCtrl8
            // 
            this.cardCtrl8.AllowDrop = true;
            this.cardCtrl8.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl8.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl8.Location = new System.Drawing.Point(55, 107);
            this.cardCtrl8.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrl8.Name = "cardCtrl8";
            this.cardCtrl8.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl8.TabIndex = 7;
            // 
            // cardCtrl7
            // 
            this.cardCtrl7.AllowDrop = true;
            this.cardCtrl7.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl7.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl7.Location = new System.Drawing.Point(3, 107);
            this.cardCtrl7.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrl7.Name = "cardCtrl7";
            this.cardCtrl7.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl7.TabIndex = 6;
            // 
            // cardCtrl6
            // 
            this.cardCtrl6.AllowDrop = true;
            this.cardCtrl6.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl6.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl6.Location = new System.Drawing.Point(107, 55);
            this.cardCtrl6.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrl6.Name = "cardCtrl6";
            this.cardCtrl6.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl6.TabIndex = 5;
            // 
            // cardCtrl5
            // 
            this.cardCtrl5.AllowDrop = true;
            this.cardCtrl5.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl5.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl5.Location = new System.Drawing.Point(55, 55);
            this.cardCtrl5.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrl5.Name = "cardCtrl5";
            this.cardCtrl5.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl5.TabIndex = 4;
            // 
            // cardCtrl4
            // 
            this.cardCtrl4.AllowDrop = true;
            this.cardCtrl4.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl4.Location = new System.Drawing.Point(3, 55);
            this.cardCtrl4.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrl4.Name = "cardCtrl4";
            this.cardCtrl4.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl4.TabIndex = 3;
            // 
            // cardCtrl3
            // 
            this.cardCtrl3.AllowDrop = true;
            this.cardCtrl3.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl3.Location = new System.Drawing.Point(107, 3);
            this.cardCtrl3.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrl3.Name = "cardCtrl3";
            this.cardCtrl3.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl3.TabIndex = 2;
            // 
            // cardCtrl2
            // 
            this.cardCtrl2.AllowDrop = true;
            this.cardCtrl2.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrl2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cardCtrl2.Location = new System.Drawing.Point(55, 3);
            this.cardCtrl2.Margin = new System.Windows.Forms.Padding(1);
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
            this.cardCtrl1.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrl1.Name = "cardCtrl1";
            this.cardCtrl1.Size = new System.Drawing.Size(50, 50);
            this.cardCtrl1.TabIndex = 0;
            // 
            // panelMarkerDeck
            // 
            this.panelMarkerDeck.BackColor = System.Drawing.Color.Turquoise;
            this.panelMarkerDeck.Controls.Add(this.panel1);
            this.panelMarkerDeck.Location = new System.Drawing.Point(685, 11);
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
            // timerTurnScan
            // 
            this.timerTurnScan.Interval = 250;
            this.timerTurnScan.Tick += new System.EventHandler(this.timerTurnScan_Tick);
            // 
            // panelDebug
            // 
            this.panelDebug.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.panelDebug.Controls.Add(this.pictureDebugScreen);
            this.panelDebug.Controls.Add(this.labelDebugDesc);
            this.panelDebug.Controls.Add(this.labelDebugTime);
            this.panelDebug.Location = new System.Drawing.Point(12, 295);
            this.panelDebug.Name = "panelDebug";
            this.panelDebug.Size = new System.Drawing.Size(454, 162);
            this.panelDebug.TabIndex = 10;
            // 
            // pictureDebugScreen
            // 
            this.pictureDebugScreen.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureDebugScreen.Location = new System.Drawing.Point(3, 39);
            this.pictureDebugScreen.Name = "pictureDebugScreen";
            this.pictureDebugScreen.Size = new System.Drawing.Size(447, 120);
            this.pictureDebugScreen.TabIndex = 10;
            this.pictureDebugScreen.TabStop = false;
            // 
            // labelDebugDesc
            // 
            this.labelDebugDesc.AutoSize = true;
            this.labelDebugDesc.BackColor = System.Drawing.Color.Transparent;
            this.labelDebugDesc.Location = new System.Drawing.Point(3, 23);
            this.labelDebugDesc.Name = "labelDebugDesc";
            this.labelDebugDesc.Size = new System.Drawing.Size(63, 13);
            this.labelDebugDesc.TabIndex = 9;
            this.labelDebugDesc.Text = "debug desc";
            // 
            // labelDebugTime
            // 
            this.labelDebugTime.AutoSize = true;
            this.labelDebugTime.BackColor = System.Drawing.Color.Transparent;
            this.labelDebugTime.Location = new System.Drawing.Point(3, 10);
            this.labelDebugTime.Name = "labelDebugTime";
            this.labelDebugTime.Size = new System.Drawing.Size(59, 13);
            this.labelDebugTime.TabIndex = 8;
            this.labelDebugTime.Text = "debug time";
            // 
            // timerDashAnim
            // 
            this.timerDashAnim.Enabled = true;
            this.timerDashAnim.Tick += new System.EventHandler(this.timerDashAnim_Tick);
            // 
            // FormOverlay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Fuchsia;
            this.ClientSize = new System.Drawing.Size(852, 473);
            this.ControlBox = false;
            this.Controls.Add(this.panelDebug);
            this.Controls.Add(this.panelDetails);
            this.Controls.Add(this.panelMarkerBoard);
            this.Controls.Add(this.panelBoard);
            this.Controls.Add(this.panelMarkerDeck);
            this.Controls.Add(this.panelSummary);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "FormOverlay";
            this.Opacity = 0.8D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "FormOverlay";
            this.TopMost = true;
            this.TransparencyKey = System.Drawing.Color.Fuchsia;
            this.panelSummary.ResumeLayout(false);
            this.panelSummary.PerformLayout();
            this.panelDetails.ResumeLayout(false);
            this.panelDetails.PerformLayout();
            this.panelMarkerBoard.ResumeLayout(false);
            this.panelBoard.ResumeLayout(false);
            this.panelMarkerDeck.ResumeLayout(false);
            this.panelDebug.ResumeLayout(false);
            this.panelDebug.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureDebugScreen)).EndInit();
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
        private System.Windows.Forms.Panel panelSummary;
        private HitInvisibleLabel labelNpc;
        private HitInvisiblePanel panelMarkerDeck;
        private HitInvisiblePanel panel1;
        private HitInvisiblePanel panelMarkerBoard;
        private HitInvisiblePanel panel3;
        private System.Windows.Forms.Button buttonCapture;
        private System.Windows.Forms.Timer timerFadeMarkers;
        private HitInvisibleLabel labelStatus;
        private System.Windows.Forms.CheckBox checkBoxDetails;
        private HitInvisiblePanel panelDetails;
        private HitInvisibleLabel hitInvisibleLabel1;
        private System.Windows.Forms.Timer timerTurnScan;
        private System.Windows.Forms.CheckBox checkBoxAutoScan;
        private HitInvisiblePanel panelDebug;
        private HitInvisibleLabel labelDebugDesc;
        private HitInvisibleLabel labelDebugTime;
        private System.Windows.Forms.PictureBox pictureDebugScreen;
        private System.Windows.Forms.Timer timerDashAnim;
    }
}