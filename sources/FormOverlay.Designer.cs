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
            this.timerTurnScan = new System.Windows.Forms.Timer(this.components);
            this.timerDashAnim = new System.Windows.Forms.Timer(this.components);
            this.panelDebug = new FFTriadBuddy.HitInvisiblePanel();
            this.pictureDebugScreen = new System.Windows.Forms.PictureBox();
            this.labelDebugDesc = new FFTriadBuddy.HitInvisibleLabel();
            this.labelDebugTime = new FFTriadBuddy.HitInvisibleLabel();
            this.panelDetails = new FFTriadBuddy.HitInvisiblePanel();
            this.label2 = new System.Windows.Forms.Label();
            this.labelScanId = new System.Windows.Forms.Label();
            this.labelUnknownPlaced = new System.Windows.Forms.Label();
            this.labelNumPlaced = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.panelDeckDetails = new System.Windows.Forms.Panel();
            this.cardCtrlRedVar4 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedVar3 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedVar2 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedVar1 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedVar0 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedKnown4 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedKnown3 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedKnown2 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedKnown1 = new FFTriadBuddy.CardCtrl();
            this.cardCtrlRedKnown0 = new FFTriadBuddy.CardCtrl();
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
            this.panelSummary.SuspendLayout();
            this.panelDebug.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureDebugScreen)).BeginInit();
            this.panelDetails.SuspendLayout();
            this.panelDeckDetails.SuspendLayout();
            this.panelMarkerBoard.SuspendLayout();
            this.panelBoard.SuspendLayout();
            this.panelMarkerDeck.SuspendLayout();
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
            // timerTurnScan
            // 
            this.timerTurnScan.Interval = 250;
            this.timerTurnScan.Tick += new System.EventHandler(this.timerTurnScan_Tick);
            // 
            // timerDashAnim
            // 
            this.timerDashAnim.Enabled = true;
            this.timerDashAnim.Tick += new System.EventHandler(this.timerDashAnim_Tick);
            // 
            // panelDebug
            // 
            this.panelDebug.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.panelDebug.Controls.Add(this.pictureDebugScreen);
            this.panelDebug.Controls.Add(this.labelDebugDesc);
            this.panelDebug.Controls.Add(this.labelDebugTime);
            this.panelDebug.Location = new System.Drawing.Point(12, 299);
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
            // panelDetails
            // 
            this.panelDetails.BackColor = System.Drawing.Color.AntiqueWhite;
            this.panelDetails.Controls.Add(this.label2);
            this.panelDetails.Controls.Add(this.labelScanId);
            this.panelDetails.Controls.Add(this.labelUnknownPlaced);
            this.panelDetails.Controls.Add(this.labelNumPlaced);
            this.panelDetails.Controls.Add(this.label1);
            this.panelDetails.Controls.Add(this.panelDeckDetails);
            this.panelDetails.Controls.Add(this.deckCtrlBlue);
            this.panelDetails.Controls.Add(this.deckCtrlRed);
            this.panelDetails.Controls.Add(this.labelNpc);
            this.panelDetails.Controls.Add(this.labelRules);
            this.panelDetails.Location = new System.Drawing.Point(12, 131);
            this.panelDetails.Name = "panelDetails";
            this.panelDetails.Size = new System.Drawing.Size(466, 162);
            this.panelDetails.TabIndex = 9;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(182, 91);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(262, 13);
            this.label2.TabIndex = 13;
            this.label2.Text = "< Potential red cards, not including visible ones below:";
            // 
            // labelScanId
            // 
            this.labelScanId.AutoSize = true;
            this.labelScanId.Location = new System.Drawing.Point(3, 4);
            this.labelScanId.Name = "labelScanId";
            this.labelScanId.Size = new System.Drawing.Size(56, 13);
            this.labelScanId.TabIndex = 12;
            this.labelScanId.Text = "Scan Id: 0";
            // 
            // labelUnknownPlaced
            // 
            this.labelUnknownPlaced.AutoSize = true;
            this.labelUnknownPlaced.Location = new System.Drawing.Point(25, 69);
            this.labelUnknownPlaced.Name = "labelUnknownPlaced";
            this.labelUnknownPlaced.Size = new System.Drawing.Size(70, 13);
            this.labelUnknownPlaced.TabIndex = 11;
            this.labelUnknownPlaced.Text = "Var placed: 0";
            // 
            // labelNumPlaced
            // 
            this.labelNumPlaced.AutoSize = true;
            this.labelNumPlaced.Location = new System.Drawing.Point(25, 56);
            this.labelNumPlaced.Name = "labelNumPlaced";
            this.labelNumPlaced.Size = new System.Drawing.Size(65, 13);
            this.labelNumPlaced.TabIndex = 10;
            this.labelNumPlaced.Text = "All placed: 0";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 42);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(90, 13);
            this.label1.TabIndex = 9;
            this.label1.Text = "Red deck details:";
            // 
            // panelDeckDetails
            // 
            this.panelDeckDetails.BackColor = System.Drawing.Color.LightCoral;
            this.panelDeckDetails.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedVar4);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedVar3);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedVar2);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedVar1);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedVar0);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedKnown4);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedKnown3);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedKnown2);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedKnown1);
            this.panelDeckDetails.Controls.Add(this.cardCtrlRedKnown0);
            this.panelDeckDetails.Location = new System.Drawing.Point(4, 88);
            this.panelDeckDetails.Name = "panelDeckDetails";
            this.panelDeckDetails.Size = new System.Drawing.Size(172, 70);
            this.panelDeckDetails.TabIndex = 8;
            // 
            // cardCtrlRedVar4
            // 
            this.cardCtrlRedVar4.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedVar4.Location = new System.Drawing.Point(137, 35);
            this.cardCtrlRedVar4.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedVar4.Name = "cardCtrlRedVar4";
            this.cardCtrlRedVar4.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedVar4.TabIndex = 9;
            // 
            // cardCtrlRedVar3
            // 
            this.cardCtrlRedVar3.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedVar3.Location = new System.Drawing.Point(103, 35);
            this.cardCtrlRedVar3.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedVar3.Name = "cardCtrlRedVar3";
            this.cardCtrlRedVar3.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedVar3.TabIndex = 8;
            // 
            // cardCtrlRedVar2
            // 
            this.cardCtrlRedVar2.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedVar2.Location = new System.Drawing.Point(69, 35);
            this.cardCtrlRedVar2.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedVar2.Name = "cardCtrlRedVar2";
            this.cardCtrlRedVar2.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedVar2.TabIndex = 7;
            // 
            // cardCtrlRedVar1
            // 
            this.cardCtrlRedVar1.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedVar1.Location = new System.Drawing.Point(35, 35);
            this.cardCtrlRedVar1.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedVar1.Name = "cardCtrlRedVar1";
            this.cardCtrlRedVar1.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedVar1.TabIndex = 6;
            // 
            // cardCtrlRedVar0
            // 
            this.cardCtrlRedVar0.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedVar0.Location = new System.Drawing.Point(1, 35);
            this.cardCtrlRedVar0.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedVar0.Name = "cardCtrlRedVar0";
            this.cardCtrlRedVar0.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedVar0.TabIndex = 5;
            // 
            // cardCtrlRedKnown4
            // 
            this.cardCtrlRedKnown4.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedKnown4.Location = new System.Drawing.Point(137, 1);
            this.cardCtrlRedKnown4.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedKnown4.Name = "cardCtrlRedKnown4";
            this.cardCtrlRedKnown4.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedKnown4.TabIndex = 4;
            // 
            // cardCtrlRedKnown3
            // 
            this.cardCtrlRedKnown3.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedKnown3.Location = new System.Drawing.Point(103, 1);
            this.cardCtrlRedKnown3.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedKnown3.Name = "cardCtrlRedKnown3";
            this.cardCtrlRedKnown3.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedKnown3.TabIndex = 3;
            // 
            // cardCtrlRedKnown2
            // 
            this.cardCtrlRedKnown2.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedKnown2.Location = new System.Drawing.Point(69, 1);
            this.cardCtrlRedKnown2.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedKnown2.Name = "cardCtrlRedKnown2";
            this.cardCtrlRedKnown2.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedKnown2.TabIndex = 2;
            // 
            // cardCtrlRedKnown1
            // 
            this.cardCtrlRedKnown1.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedKnown1.Location = new System.Drawing.Point(35, 1);
            this.cardCtrlRedKnown1.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedKnown1.Name = "cardCtrlRedKnown1";
            this.cardCtrlRedKnown1.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedKnown1.TabIndex = 1;
            // 
            // cardCtrlRedKnown0
            // 
            this.cardCtrlRedKnown0.BackColor = System.Drawing.SystemColors.Control;
            this.cardCtrlRedKnown0.Location = new System.Drawing.Point(1, 1);
            this.cardCtrlRedKnown0.Margin = new System.Windows.Forms.Padding(1);
            this.cardCtrlRedKnown0.Name = "cardCtrlRedKnown0";
            this.cardCtrlRedKnown0.Size = new System.Drawing.Size(32, 32);
            this.cardCtrlRedKnown0.TabIndex = 0;
            // 
            // deckCtrlBlue
            // 
            this.deckCtrlBlue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(150)))), ((int)(((byte)(166)))));
            this.deckCtrlBlue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.deckCtrlBlue.Location = new System.Drawing.Point(182, 4);
            this.deckCtrlBlue.Name = "deckCtrlBlue";
            this.deckCtrlBlue.Size = new System.Drawing.Size(280, 51);
            this.deckCtrlBlue.TabIndex = 0;
            // 
            // deckCtrlRed
            // 
            this.deckCtrlRed.BackColor = System.Drawing.Color.LightCoral;
            this.deckCtrlRed.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.deckCtrlRed.Location = new System.Drawing.Point(182, 107);
            this.deckCtrlRed.Name = "deckCtrlRed";
            this.deckCtrlRed.Size = new System.Drawing.Size(280, 51);
            this.deckCtrlRed.TabIndex = 1;
            // 
            // labelNpc
            // 
            this.labelNpc.AutoSize = true;
            this.labelNpc.BackColor = System.Drawing.Color.Transparent;
            this.labelNpc.Location = new System.Drawing.Point(182, 58);
            this.labelNpc.Name = "labelNpc";
            this.labelNpc.Size = new System.Drawing.Size(79, 13);
            this.labelNpc.TabIndex = 7;
            this.labelNpc.Text = "NPC: unknown";
            // 
            // labelRules
            // 
            this.labelRules.AutoSize = true;
            this.labelRules.BackColor = System.Drawing.Color.Transparent;
            this.labelRules.Location = new System.Drawing.Point(182, 71);
            this.labelRules.Name = "labelRules";
            this.labelRules.Size = new System.Drawing.Size(84, 13);
            this.labelRules.TabIndex = 5;
            this.labelRules.Text = "Rules: unknown";
            // 
            // panelMarkerBoard
            // 
            this.panelMarkerBoard.BackColor = System.Drawing.Color.White;
            this.panelMarkerBoard.Controls.Add(this.panel3);
            this.panelMarkerBoard.Location = new System.Drawing.Point(304, 12);
            this.panelMarkerBoard.Name = "panelMarkerBoard";
            this.panelMarkerBoard.Padding = new System.Windows.Forms.Padding(5);
            this.panelMarkerBoard.Size = new System.Drawing.Size(60, 50);
            this.panelMarkerBoard.TabIndex = 8;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.Color.Fuchsia;
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(5, 5);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(50, 40);
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
            this.panelBoard.Location = new System.Drawing.Point(509, 131);
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
            this.panelMarkerDeck.Location = new System.Drawing.Point(240, 12);
            this.panelMarkerDeck.Name = "panelMarkerDeck";
            this.panelMarkerDeck.Padding = new System.Windows.Forms.Padding(5);
            this.panelMarkerDeck.Size = new System.Drawing.Size(58, 50);
            this.panelMarkerDeck.TabIndex = 7;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.Fuchsia;
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(5, 5);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(48, 40);
            this.panel1.TabIndex = 0;
            // 
            // FormOverlay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Fuchsia;
            this.ClientSize = new System.Drawing.Size(713, 473);
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
            this.panelDebug.ResumeLayout(false);
            this.panelDebug.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureDebugScreen)).EndInit();
            this.panelDetails.ResumeLayout(false);
            this.panelDetails.PerformLayout();
            this.panelDeckDetails.ResumeLayout(false);
            this.panelMarkerBoard.ResumeLayout(false);
            this.panelBoard.ResumeLayout(false);
            this.panelMarkerDeck.ResumeLayout(false);
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
        private System.Windows.Forms.Label labelUnknownPlaced;
        private System.Windows.Forms.Label labelNumPlaced;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panelDeckDetails;
        private CardCtrl cardCtrlRedVar4;
        private CardCtrl cardCtrlRedVar3;
        private CardCtrl cardCtrlRedVar2;
        private CardCtrl cardCtrlRedVar1;
        private CardCtrl cardCtrlRedVar0;
        private CardCtrl cardCtrlRedKnown4;
        private CardCtrl cardCtrlRedKnown3;
        private CardCtrl cardCtrlRedKnown2;
        private CardCtrl cardCtrlRedKnown1;
        private CardCtrl cardCtrlRedKnown0;
        private System.Windows.Forms.Label labelScanId;
        private System.Windows.Forms.Label label2;
    }
}