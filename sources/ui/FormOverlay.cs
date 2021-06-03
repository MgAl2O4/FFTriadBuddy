using MgAl2O4.Utils;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public partial class FormOverlay : Form
    {
        private ImageList cardImages;
        private CardCtrl[] boardControls;
        private CardCtrl[] redDeckKnownCards;
        private CardCtrl[] redDeckUnknownCards;
        private HitInvisibleLabel[] cactpotBoard;
        private bool bHasValidMarkerDeck;
        private bool bHasValidMarkerBoard;
        private bool bCanDrawCaptureMarker;
        private bool bCanAdjustSummaryLocation;
        private bool bCanStopTurnScan;
        private bool bCanAutoCapture;
        private int dashAnimOffset;
        private int scanId;
        private Point summaryMovePt;
        private Point cactpotLinePt0;
        private Point cactpotLinePt1;

        private TriadGameScreenMemory screenMemory;
        public ScreenAnalyzer screenAnalyzer;
        private TriadNpc npc;

        public delegate void UpdateStateDelegate();
        public event UpdateStateDelegate OnUpdateState;

        public FormOverlay()
        {
            InitializeComponent();
            ApplyLocalization();

            cactpotBoard = new HitInvisibleLabel[9] { labelCactpot0, labelCactpot1, labelCactpot2, labelCactpot3, labelCactpot4, labelCactpot5, labelCactpot6, labelCactpot7, labelCactpot8 };
            boardControls = new CardCtrl[9] { cardCtrl1, cardCtrl2, cardCtrl3, cardCtrl4, cardCtrl5, cardCtrl6, cardCtrl7, cardCtrl8, cardCtrl9 };
            for (int Idx = 0; Idx < boardControls.Length; Idx++)
            {
                boardControls[Idx].defaultBackColor = panelBoard.BackColor;
                boardControls[Idx].drawMode = ECardDrawMode.OwnerOnly;
                boardControls[Idx].bBlinkHighlighted = false;
                boardControls[Idx].bEnableHitTest = false;
                boardControls[Idx].SetCard(null);
                boardControls[Idx].Tag = Idx;
            }

            redDeckKnownCards = new CardCtrl[5] { cardCtrlRedKnown0, cardCtrlRedKnown1, cardCtrlRedKnown2, cardCtrlRedKnown3, cardCtrlRedKnown4 };
            redDeckUnknownCards = new CardCtrl[5] { cardCtrlRedVar0, cardCtrlRedVar1, cardCtrlRedVar2, cardCtrlRedVar3, cardCtrlRedVar4 };
            for (int Idx = 0; Idx < redDeckKnownCards.Length; Idx++)
            {
                redDeckKnownCards[Idx].defaultBackColor = panelDeckDetails.BackColor;
                redDeckKnownCards[Idx].drawMode = ECardDrawMode.ImageOnly;
                redDeckKnownCards[Idx].bBlinkHighlighted = false;
                redDeckKnownCards[Idx].bEnableHitTest = false;
                redDeckKnownCards[Idx].SetCard(null);
                redDeckKnownCards[Idx].Tag = Idx;

                redDeckUnknownCards[Idx].defaultBackColor = panelDeckDetails.BackColor;
                redDeckUnknownCards[Idx].drawMode = ECardDrawMode.ImageOnly;
                redDeckUnknownCards[Idx].bBlinkHighlighted = false;
                redDeckUnknownCards[Idx].bEnableHitTest = false;
                redDeckUnknownCards[Idx].SetCard(null);
                redDeckUnknownCards[Idx].Tag = Idx;
            }

            panelMarkerBoard.Visible = false;
            panelMarkerDeck.Visible = false;
            panelMarkerSwap.Visible = false;
            panelMarkerLine.Visible = false;
            panelDetails.Visible = false;
            panelBoard.Visible = false;
            panelCactpot.Visible = false;
            panelDebug.Visible = false;
            panelSwapWarning.Visible = false;
            panelScanResolution.Visible = false;
            labelStatus.Focus();
            labelSwapWarningIcon.Image = SystemIcons.Warning.ToBitmap();

            bHasValidMarkerDeck = false;
            bHasValidMarkerBoard = false;
            bCanAdjustSummaryLocation = false;
            bCanStopTurnScan = true;
            bCanAutoCapture = false;
            dashAnimOffset = 0;
            scanId = 0;
            screenMemory = new TriadGameScreenMemory();

            Location = Screen.PrimaryScreen.Bounds.Location;
            Size = Screen.PrimaryScreen.Bounds.Size;
            UpdateOverlayLocation(10, 10);
            UpdateAutoCaptureMarker();
            UpdateStatusDescription();
        }

        private void ApplyLocalization()
        {
            buttonCapture.Text = loc.strings.OverlayForm_Capture_Button;
            labelStatus.Text = loc.strings.OverlayForm_Capture_Status;
            checkBoxDetails.Text = loc.strings.OverlayForm_Capture_Details;
            checkBoxAutoScan.Text = loc.strings.OverlayForm_Capture_AutoScan;
            hitInvisibleLabel2.Text = loc.strings.OverlayForm_CardInfo_Swapped;
            labelSwapWarningText.Text = loc.strings.OverlayForm_DeckInfo_Mismatch;
            labelScanId.Text = string.Format(loc.strings.OverlayForm_Details_ScanId, 0);
            label1.Text = loc.strings.OverlayForm_Details_RedDeck;
            labelNumPlaced.Text = string.Format(loc.strings.OverlayForm_Details_RedPlacedAll, 0);
            labelUnknownPlaced.Text = string.Format(loc.strings.OverlayForm_Details_RedPlacedVariable, 0);
            labelNpc.Text = string.Format(loc.strings.OverlayForm_Details_Npc, loc.strings.OverlayForm_Dynamic_NpcUnknown);
            labelRules.Text = string.Format(loc.strings.OverlayForm_Details_Rules, loc.strings.OverlayForm_Dynamic_NpcUnknown);
            label2.Text = loc.strings.OverlayForm_Details_RedInfo;
        }

        public void InitializeAssets(ImageList cardImageList)
        {
            cardImages = cardImageList;

            for (int Idx = 0; Idx < boardControls.Length; Idx++)
            {
                boardControls[Idx].cardIcons = cardImages;
            }
            for (int Idx = 0; Idx < redDeckKnownCards.Length; Idx++)
            {
                redDeckKnownCards[Idx].cardIcons = cardImages;
                redDeckUnknownCards[Idx].cardIcons = cardImages;
            }

            deckCtrlBlue.cardIcons = cardImages;
            deckCtrlRed.cardIcons = cardImages;
            deckCtrlBlue.allowRearrange = false;
            deckCtrlRed.allowRearrange = false;
            deckCtrlBlue.enableHitTest = false;
            deckCtrlRed.enableHitTest = false;
            deckCtrlBlue.drawMode = ECardDrawMode.ImageOnly;
            deckCtrlRed.drawMode = ECardDrawMode.ImageOnly;
            deckCtrlBlue.clickAction = EDeckCtrlAction.None;
            deckCtrlRed.clickAction = EDeckCtrlAction.None;
            deckCtrlBlue.deckOwner = ETriadCardOwner.Blue;
            deckCtrlRed.deckOwner = ETriadCardOwner.Red;

            checkBoxAutoScan.Checked = PlayerSettingsDB.Get().useAutoScan;
        }

        public void UpdatePlayerDeck(TriadDeck activeDeck)
        {
            screenMemory.UpdatePlayerDeck(activeDeck);
        }

        public void UpdateScreenState(bool bDebugMode = false)
        {
            checkBoxDetails_CheckedChanged(null, null);

            if (screenAnalyzer.GetCurrentState() != ScreenAnalyzer.EState.NoErrors)
            {
                Logger.WriteLine("Capture failed: " + screenAnalyzer.GetCurrentState());
                bHasValidMarkerDeck = false;
                bHasValidMarkerBoard = false;
                UpdateStatusDescription();
                return;
            }

            scanId++;
            labelScanId.Text = string.Format(loc.strings.OverlayForm_Details_ScanId, scanId);
            Logger.WriteLine("Capture " + labelScanId.Text);

            // multi monitor setup: make sure that overlay and game and on the same monitor
            Rectangle gameWindowRect = screenAnalyzer.screenReader.GetCachedGameWindow();
            if (gameWindowRect.Width > 0)
            {
                Rectangle gameScreenBounds = Screen.GetBounds(gameWindowRect);
                Point centerPt = new Point((Left + Right) / 2, (Top + Bottom) / 2);
                if (!gameScreenBounds.Contains(centerPt))
                {
                    Location = gameScreenBounds.Location;
                    Size = gameScreenBounds.Size;
                    bCanAdjustSummaryLocation = true;
                }
            }

            int markerTimeout = timerFadeMarkers.Interval;

            TriadGameScreenMemory.EUpdateFlags updateFlags = TriadGameScreenMemory.EUpdateFlags.None;
            if (screenAnalyzer.activeScanner is ScannerTriad)
            {
                markerTimeout = 4000;

                // update overlay locations
                if (bCanAdjustSummaryLocation)
                {
                    Rectangle gridRect = screenAnalyzer.scannerTriad.GetBoardBox();
                    if ((gridRect.Width > 0) && (gameWindowRect.Width > 0))
                    {
                        bCanAdjustSummaryLocation = false;

                        Rectangle boardBoundsLocal = ConvertGameBoundsToLocal(gridRect, 0);
                        int boardLocalMidX = (boardBoundsLocal.Left + boardBoundsLocal.Right) / 2;
                        UpdateOverlayLocation(boardLocalMidX - (panelSummary.Width / 2), boardBoundsLocal.Bottom + 50);
                    }
                }

                // solver logic
                updateFlags = screenMemory.OnNewScan(screenAnalyzer.scannerTriad.cachedGameState, npc);
                if (updateFlags != TriadGameScreenMemory.EUpdateFlags.None)
                {
                    FindNextMove(out int markerDeckPos, out int markerBoardPos, out TriadGameResultChance bestChance);
                    ETriadGameState expectedResult = bestChance.expectedResult;

                    TriadCard suggestedCard = screenMemory.deckBlue.GetCard(markerDeckPos);
                    Logger.WriteLine("  suggested move: [" + markerBoardPos + "] " + ETriadCardOwner.Blue + " " + (suggestedCard != null ? suggestedCard.Name : "??") + " (expected: " + expectedResult + ")");

                    bHasValidMarkerDeck = false;
                    bHasValidMarkerBoard = false;
                    if (markerDeckPos >= 0 && markerBoardPos >= 0)
                    {
                        try
                        {
                            Rectangle rectDeckPos = screenAnalyzer.scannerTriad.GetBlueCardBox(markerDeckPos);
                            Rectangle rectBoardPos = screenAnalyzer.scannerTriad.GetBoardCardBox(markerBoardPos);

                            panelMarkerDeck.Bounds = ConvertGameBoundsToLocal(rectDeckPos);
                            panelMarkerBoard.Bounds = ConvertGameBoundsToLocal(rectBoardPos);
                            panelMarkerBoard.BackColor =
                                (expectedResult == ETriadGameState.BlueWins) ? Color.Lime :
                                (expectedResult == ETriadGameState.BlueDraw) ? Color.Gold :
                                Color.Red;

                            bHasValidMarkerDeck = true;
                            bHasValidMarkerBoard = true;
                        }
                        catch (Exception) { }
                    }
                }
            }
            else if (screenAnalyzer.activeScanner is ScannerCactpot)
            {
                markerTimeout = 2000;

                // update overlay locations
                if (bCanAdjustSummaryLocation)
                {
                    Rectangle boardRect = screenAnalyzer.scannerCactpot.GetBoardBox();
                    if ((boardRect.Width > 0) && (gameWindowRect.Width > 0))
                    {
                        bCanAdjustSummaryLocation = false;
                        boardRect.Offset(0, 50);

                        Rectangle boardBoundsLocal = ConvertGameBoundsToLocal(boardRect, 0);
                        int boardLocalMidX = (boardBoundsLocal.Left + boardBoundsLocal.Right) / 2;
                        UpdateOverlayLocation(boardLocalMidX - (panelSummary.Width / 2), boardBoundsLocal.Bottom + 50);
                    }
                }

                // solver logic
                if (screenAnalyzer.scannerCactpot.cachedGameState.numRevealed > 3)
                {
                    bHasValidMarkerBoard = false;

                    CactpotGame.FindBestLine(screenAnalyzer.scannerCactpot.cachedGameState.board, out int fromIdx, out int toIdx);
                    Logger.WriteLine("  suggested line: [" + fromIdx + "] -> [" + toIdx + "]");

                    if (fromIdx >= 0 && toIdx >= 0)
                    {
                        Rectangle gameFromBox = screenAnalyzer.scannerCactpot.GetCircleBox(fromIdx);
                        Rectangle gameToBox = screenAnalyzer.scannerCactpot.GetCircleBox(toIdx);

                        Rectangle localFromBox = ConvertGameBoundsToLocal(gameFromBox);
                        Rectangle localToBox = ConvertGameBoundsToLocal(gameToBox);
                        ShowCactpotLine(localFromBox, localToBox);
                    }
                }
                else
                {
                    int markerPos = CactpotGame.FindNextCircle(screenAnalyzer.scannerCactpot.cachedGameState.board);
                    Logger.WriteLine("  suggested move: [" + markerPos + "]");

                    bHasValidMarkerBoard = (markerPos >= 0);
                    if (bHasValidMarkerBoard)
                    {
                        Rectangle gameBoardPos = screenAnalyzer.scannerCactpot.GetCircleBox(markerPos);
                        panelMarkerBoard.Bounds = ConvertGameBoundsToLocal(gameBoardPos);
                        panelMarkerBoard.BackColor = Color.Lime;
                    }
                }

                for (int Idx = 0; Idx < screenAnalyzer.scannerCactpot.cachedGameState.board.Length; Idx++)
                {
                    int numInCircle = screenAnalyzer.scannerCactpot.cachedGameState.board[Idx];
                    cactpotBoard[Idx].Text = (numInCircle == 0) ? "" : numInCircle.ToString();
                }
            }

            // update what's needed
            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.Modifiers) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                string desc = "";
                foreach (TriadGameModifier mod in screenMemory.gameSession.modifiers)
                {
                    desc += mod.ToString() + ", ";
                }

                labelRules.Text = string.Format(loc.strings.OverlayForm_Details_Rules,
                    (desc.Length > 2) ? desc.Remove(desc.Length - 2, 2) : loc.strings.OverlayForm_Dynamic_NpcUnknown);
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.BlueDeck) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                deckCtrlBlue.SetDeck(screenAnalyzer.scannerTriad.cachedGameState.blueDeck);
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.RedDeck) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                deckCtrlRed.SetDeck(screenAnalyzer.scannerTriad.cachedGameState.redDeck);
                UpdateRedDeckDetails(screenMemory.deckRed);
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.Board) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                for (int Idx = 0; Idx < screenMemory.gameState.board.Length; Idx++)
                {
                    boardControls[Idx].SetCard(screenMemory.gameState.board[Idx]);
                }
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.SwapWarning) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                Rectangle ruleRect = screenAnalyzer.scannerTriad.GetRuleBox();
                if (gameWindowRect.Width > 0 && ruleRect.Width > 0)
                {
                    Rectangle gameWarningBounds = new Rectangle(ruleRect.Left, ruleRect.Top - panelSwapWarning.Height - 10, 0, 0);
                    panelSwapWarning.Location = ConvertGameBoundsToLocal(gameWarningBounds, 0).Location;
                    panelSwapWarning.Visible = true;
                    timerHideSwapWarning.Stop();
                    timerHideSwapWarning.Start();
                }
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.SwapHints) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                Rectangle gameDeckPos = screenAnalyzer.scannerTriad.GetBlueCardBox(screenMemory.swappedBlueCardIdx);
                panelMarkerSwap.Bounds = ConvertGameBoundsToLocal(gameDeckPos, -10);
                panelMarkerSwap.Visible = true;
            }

            bCanStopTurnScan = false;
            bCanAutoCapture = false;

            panelMarkerDeck.Visible = bHasValidMarkerDeck;
            panelMarkerBoard.Visible = bHasValidMarkerBoard;

            if (bHasValidMarkerDeck || bHasValidMarkerBoard || panelMarkerSwap.Visible || panelMarkerLine.Visible)
            {
                timerFadeMarkers.Interval = markerTimeout;
                timerFadeMarkers.Enabled = true;
            }
            else
            {
                timerFadeMarkers.Enabled = false;
            }

            timerTurnScan.Enabled = true;
            timerTurnScan_Tick(null, null);
        }

        private void FindNextMove(out int blueCardIdx, out int boardCardIdx, out TriadGameResultChance bestChance)
        {
            boardCardIdx = -1;

            screenMemory.gameSession.SolverFindBestMove(screenMemory.gameState, out int solverBoardPos, out TriadCard solverTriadCard, out bestChance);

            blueCardIdx = screenMemory.deckBlue.GetCardIndex(solverTriadCard);
            if (blueCardIdx >= 0)
            {
                boardCardIdx = solverBoardPos;
            }
        }

        private Rectangle ConvertGameBoundsToLocal(Rectangle gameBounds, int inflateSize = 10)
        {
            Rectangle localBounds = screenAnalyzer.ConvertGameToScreen(gameBounds);
            localBounds.X -= Location.X;
            localBounds.Y -= Location.Y;
            localBounds.Inflate(inflateSize, inflateSize);

            return localBounds;
        }

        private bool IsCursorInScanArea()
        {
            Rectangle screenScanArea = screenAnalyzer.ConvertGameToScreen(screenAnalyzer.currentScanArea);
            return screenScanArea.Contains(Cursor.Position);
        }

        private void buttonCaptureWoker()
        {
            screenAnalyzer.DoWork(ScreenAnalyzer.EMode.Default);
            UpdateScreenState();
            OnUpdateState.Invoke();
        }

        private void buttonCapture_Click(object sender, EventArgs e)
        {
            buttonCaptureWoker();
        }

        private void timerFadeMarkers_Tick(object sender, EventArgs e)
        {
            panelMarkerDeck.Visible = false;
            panelMarkerBoard.Visible = false;
            panelMarkerSwap.Visible = false;
            panelMarkerLine.Visible = false;
            timerFadeMarkers.Enabled = false;
        }

        private void checkBoxDetails_CheckedChanged(object sender, EventArgs e)
        {
            bool bShowTriadControls = (screenAnalyzer == null || screenAnalyzer.activeScanner is ScannerTriad);

            panelDetails.Visible = checkBoxDetails.Checked && bShowTriadControls;
            panelBoard.Visible = checkBoxDetails.Checked && bShowTriadControls;
            panelCactpot.Visible = checkBoxDetails.Checked && !bShowTriadControls;

            // keep panelScanResolution disabled
            // panelScanResolution.Visible = checkBoxDetails.Checked;
        }

        public bool IsUsingAutoScan()
        {
            bool bIsAutoScanAllowed = (screenAnalyzer == null || screenAnalyzer.activeScanner is ScannerTriad);
            return checkBoxAutoScan.Checked && bIsAutoScanAllowed;
        }

        public void SetNpc(TriadNpc inNpc)
        {
            npc = inNpc;

            UpdateStatusDescription();

            labelNpc.Text = string.Format(loc.strings.OverlayForm_Details_Npc, (npc != null) ? npc.ToString() : loc.strings.OverlayForm_Dynamic_NpcUnknown);
            labelRules.Text = string.Format(loc.strings.OverlayForm_Details_Rules, loc.strings.OverlayForm_Dynamic_RulesWaiting);
        }

        public void UpdateOverlayLocation(int localX, int localY)
        {
            localX = Math.Min(Math.Max(localX, 0), Size.Width - panelSummary.Width);
            localY = Math.Min(Math.Max(localY, 0), Size.Height - panelSummary.Height);

            panelSummary.Location = new Point(localX, localY);

            panelDetails.Location = new Point(localX - panelDetails.Width - 10, localY);
            panelBoard.Location = new Point(localX + panelSummary.Width + 10, localY);
            panelCactpot.Location = new Point(localX + panelSummary.Width + 10, localY);
            panelDebug.Location = new Point(localX, localY + panelSummary.Height + 10);
            panelScanResolution.Location = new Point(localX, localY + panelSummary.Height + 10);
        }

        public void InitOverlayLocation(Rectangle mainWindowBounds)
        {
            // multi monitor setup: make sure that overlay and game and on the same monitor
            Rectangle gameWindowBounds = mainWindowBounds;
            if (screenAnalyzer != null)
            {
                Rectangle testBounds = screenAnalyzer.screenReader.GetCachedGameWindow();
                if (testBounds.Width > 0)
                {
                    gameWindowBounds = testBounds;
                }
            }

            Rectangle gameScreenBounds = Screen.GetBounds(gameWindowBounds);
            Point myCenterPt = new Point((Left + Right) / 2, (Top + Bottom) / 2);
            if (!gameScreenBounds.Contains(myCenterPt))
            {
                Location = gameScreenBounds.Location;
                Size = gameScreenBounds.Size;
            }

            UpdateOverlayLocation((Width - panelSummary.Width) / 2, Bottom - panelSummary.Height - 10);
            UpdateStatusDescription();

            // allow one time auto placement on successful scan
            bCanAdjustSummaryLocation = true;
        }

        private void SetStatusText(string statusText, Icon statusIcon)
        {
            labelStatus.Text = statusText;
            hitInvisibleLabel1.Image = statusIcon.ToBitmap();
        }

        private void UpdateStatusDescription()
        {
            if (screenAnalyzer == null)
            {
                return;
            }

            ScreenAnalyzer.EState showState = screenAnalyzer.GetCurrentState();
            switch (showState)
            {
                case ScreenAnalyzer.EState.NoInputImage:
                    switch (screenAnalyzer.screenReader.currentState)
                    {
                        case ScreenReader.EState.MissingGameProcess: SetStatusText(loc.strings.OverlayForm_Dynamic_Status_MissingGameProcess, SystemIcons.Error); break;
                        case ScreenReader.EState.MissingGameWindow: SetStatusText(loc.strings.OverlayForm_Dynamic_Status_MissingGameWindow, SystemIcons.Error); break;
                        default: SetStatusText(loc.strings.OverlayForm_Dynamic_Status_NoInputImage, SystemIcons.Error); break;
                    }
                    break;

                case ScreenAnalyzer.EState.NoScannerMatch:
                    SetStatusText(loc.strings.OverlayForm_Dynamic_Status_NoScannerMatch, SystemIcons.Error);
                    break;

                case ScreenAnalyzer.EState.UnknownHash:
                    SetStatusText(loc.strings.OverlayForm_Dynamic_Status_UnknownHash, SystemIcons.Warning);
                    break;

                case ScreenAnalyzer.EState.ScannerErrors:
                    SetStatusText(loc.strings.OverlayForm_Dynamic_Status_ScannerErrors, SystemIcons.Error);
                    if (screenAnalyzer.activeScanner is ScannerTriad)
                    {
                        switch (screenAnalyzer.scannerTriad.cachedScanError)
                        {
                            case ScannerTriad.EScanError.MissingGrid: SetStatusText(loc.strings.OverlayForm_Dynamic_Status_MissingGrid, SystemIcons.Error); break;
                            case ScannerTriad.EScanError.MissingCards: SetStatusText(loc.strings.OverlayForm_Dynamic_Status_MissingCards, SystemIcons.Error); break;
                            case ScannerTriad.EScanError.FailedCardMatching: SetStatusText(loc.strings.OverlayForm_Dynamic_Status_FailedCardMatching, SystemIcons.Warning); break;
                            default: break;
                        }
                    }
                    break;

                default:
                    if (screenAnalyzer.activeScanner == null || screenAnalyzer.activeScanner.cachedGameStateBase == null)
                    {
                        string npcDesc = (npc != null) ? (npc.Name + ": ") : "";
                        SetStatusText(npcDesc + loc.strings.OverlayForm_Dynamic_Status_Ready, SystemIcons.Information);
                    }
                    else if (screenAnalyzer.activeScanner is ScannerTriad)
                    {
                        string npcDesc = (npc != null) ? (npc.Name + ": ") : "";
                        switch (screenAnalyzer.scannerTriad.cachedGameState.turnState)
                        {
                            case ScannerTriad.ETurnState.MissingTimer: SetStatusText(npcDesc + loc.strings.OverlayForm_Dynamic_Status_Ready, SystemIcons.Information); break;
                            case ScannerTriad.ETurnState.Waiting: SetStatusText(npcDesc + loc.strings.OverlayForm_Dynamic_Status_WaitingForTurn, SystemIcons.Shield); break;
                            default:
                                if (IsUsingAutoScan() && IsCursorInScanArea())
                                {
                                    SetStatusText(loc.strings.OverlayForm_Dynamic_Status_AutoScanMouseOverBoard, SystemIcons.Warning);
                                }
                                else
                                {
                                    SetStatusText(npcDesc + loc.strings.OverlayForm_Dynamic_Status_ActiveTurn, SystemIcons.Information);
                                }
                                break;
                        }
                    }
                    else if (screenAnalyzer.activeScanner is ScannerCactpot)
                    {
                        SetStatusText(loc.strings.OverlayForm_Dynamic_Status_CactpotReady, SystemIcons.Information);
                    }
                    else
                    {
                        string npcDesc = (npc != null) ? (npc.Name + ": ") : "";
                        SetStatusText(npcDesc + loc.strings.OverlayForm_Dynamic_Status_Ready, SystemIcons.Information);
                    }
                    break;
            }
        }

        private void panelSummary_MouseDown(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != 0)
            {
                summaryMovePt = e.Location;
                Cursor = Cursors.SizeAll;
            }
        }

        private void panelSummary_MouseUp(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != 0)
            {
                Cursor = Cursors.Default;
            }
        }

        private void panelSummary_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) != 0)
            {
                UpdateOverlayLocation(panelSummary.Left + e.Location.X - summaryMovePt.X, panelSummary.Top + e.Location.Y - summaryMovePt.Y);
            }
        }

        private void timerTurnScan_Tick(object sender, EventArgs e)
        {
            bool bDebugMode = false;
            UpdateAutoCaptureMarker();

            bool attemptScan = (screenAnalyzer != null) && (screenAnalyzer.activeScanner == null || screenAnalyzer.activeScanner is ScannerTriad);
            if (attemptScan)
            {
                if (timerAutoScanUpkeep.Enabled)
                {
                    // always retry in upkeep mode
                }
                else if (screenAnalyzer.scannerTriad.cachedGameState == null ||
                    bCanStopTurnScan && screenAnalyzer.scannerTriad.cachedGameState.turnState == ScannerTriad.ETurnState.MissingTimer)
                {
                    attemptScan = false;
                    bCanAutoCapture = false;

                    if (Visible && checkBoxAutoScan.Checked)
                    {
                        Logger.WriteLine("Auto scan: entering upkeep mode");
                        timerAutoScanUpkeep.Start();
                    }
                    else
                    {
                        timerTurnScan.Stop();
                    }
                }
            }

            if (attemptScan)
            {
                Rectangle timerGameBox = screenAnalyzer.scannerTriad.GetTimerScanBox();
                screenAnalyzer.scanClipBounds = timerGameBox;

                screenAnalyzer.DoWork(ScreenAnalyzer.EMode.ScanTriad | ScreenAnalyzer.EMode.NeverResetCache, (int)ScannerTriad.EScanMode.TimerOnly);

                screenAnalyzer.scanClipBounds = Rectangle.Empty;
                bCanStopTurnScan = true;
            }

            UpdateStatusDescription();
            if (bDebugMode) { UpdateDebugDetails(); }

            if (screenAnalyzer != null && screenAnalyzer.scannerTriad.cachedGameState != null && IsUsingAutoScan())
            {
                ScannerTriad.ETurnState turnState = screenAnalyzer.scannerTriad.cachedGameState.turnState;
                if (turnState != ScannerTriad.ETurnState.MissingTimer && timerAutoScanUpkeep.Enabled)
                {
                    Logger.WriteLine("Auto scan: aborting upkeep mode (scanned)");
                    timerAutoScanUpkeep.Stop();
                }

                if (turnState == ScannerTriad.ETurnState.Waiting)
                {
                    bCanAutoCapture = true;
                }
                else if (turnState == ScannerTriad.ETurnState.Active)
                {
                    if (bCanAutoCapture)
                    {
                        bool bIsMouseOverGrid = IsCursorInScanArea();
                        if (bDebugMode) { Logger.WriteLine("Checking auto scan: mouse:{0}, state:{1}", bIsMouseOverGrid ? "OverGrid" : "ok", screenAnalyzer.GetCurrentState()); }

                        if (!bIsMouseOverGrid && screenAnalyzer.GetCurrentState() == ScreenAnalyzer.EState.NoErrors)
                        {
                            bCanAutoCapture = false;
                            buttonCapture_Click(null, null);
                        }
                    }
                }
            }
        }

        private void UpdateDebugDetails()
        {
#if DEBUG
            if (screenAnalyzer != null)
            {
                panelDebug.Visible = true;
                labelDebugTime.Text = DateTime.Now.ToString("HH:mm:ss.ff");
                labelDebugDesc.Text = "Status: " + screenAnalyzer.scannerTriad.cachedGameState.turnState;
                //pictureDebugScreen.Image = screenAnalyzer.GetDebugScreenshot();
            }
#endif // DEBUG
        }

        private void UpdateRedDeckDetails(TriadDeckInstanceScreen deck)
        {
            if (deck == null || deck.deck == null)
            {
                for (int Idx = 0; Idx < redDeckKnownCards.Length; Idx++)
                {
                    redDeckKnownCards[Idx].Visible = false;
                    redDeckUnknownCards[Idx].Visible = false;
                    deckCtrlRed.SetTransparent(Idx, false);
                }

                labelNumPlaced.Text = string.Format(loc.strings.OverlayForm_Details_RedPlacedAll, "0");
                labelUnknownPlaced.Text = string.Format(loc.strings.OverlayForm_Details_RedPlacedVariable, "0");
            }
            else
            {
                int firstKnownIdx = deck.cards.Length;
                int firstUnknownIdx = deck.cards.Length + deck.deck.knownCards.Count;
                for (int Idx = 0; Idx < redDeckKnownCards.Length; Idx++)
                {
                    bool bIsValidKnownCard = Idx < deck.deck.knownCards.Count;
                    redDeckKnownCards[Idx].Visible = bIsValidKnownCard;
                    if (bIsValidKnownCard)
                    {
                        bool bIsUsed = deck.IsPlaced(firstKnownIdx + Idx);
                        redDeckKnownCards[Idx].bIsTransparent = bIsUsed;
                        TriadCard showCard = (deck.swappedCardIdx == (firstKnownIdx + Idx)) ? deck.swappedCard : deck.deck.knownCards[Idx];
                        redDeckKnownCards[Idx].SetCard(new TriadCardInstance(showCard, ETriadCardOwner.Red));
                    }

                    bool bIsValidUnknownCard = Idx < deck.deck.unknownCardPool.Count;
                    redDeckUnknownCards[Idx].Visible = bIsValidUnknownCard;
                    if (bIsValidUnknownCard)
                    {
                        bool bIsUsed = deck.IsPlaced(firstUnknownIdx + Idx);
                        redDeckUnknownCards[Idx].bIsTransparent = bIsUsed;
                        TriadCard showCard = (deck.swappedCardIdx == (firstUnknownIdx + Idx)) ? deck.swappedCard : deck.deck.unknownCardPool[Idx];
                        redDeckUnknownCards[Idx].SetCard(new TriadCardInstance(showCard, ETriadCardOwner.Red));
                    }
                }

                for (int Idx = 0; Idx < deck.cards.Length; Idx++)
                {
                    bool bIsUsed = deck.IsPlaced(Idx);
                    deckCtrlRed.SetTransparent(Idx, bIsUsed);
                }

                labelNumPlaced.Text = string.Format(loc.strings.OverlayForm_Details_RedPlacedAll, deck.numPlaced);
                labelUnknownPlaced.Text = string.Format(loc.strings.OverlayForm_Details_RedPlacedVariable, deck.numUnknownPlaced);
            }
        }

        private void UpdateAutoCaptureMarker()
        {
            bool bCanShow = IsUsingAutoScan() && (screenAnalyzer != null);
            if (bCanShow)
            {
                if (timerAutoScanUpkeep.Enabled)
                {
                    // always show during upkeep
                }
                else if (screenAnalyzer.scannerTriad.cachedGameState != null)
                {
                    bCanShow = (screenAnalyzer.scannerTriad.cachedGameState.turnState != ScannerTriad.ETurnState.MissingTimer);
                }
            }

            if (bCanShow != bCanDrawCaptureMarker)
            {
                bCanDrawCaptureMarker = bCanShow;
                timerDashAnim.Enabled = checkBoxAutoScan.Checked;
                buttonCapture.Invalidate();
            }
        }

        private void checkBoxAutoScan_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAutoCaptureMarker();

            if (timerAutoScanUpkeep.Enabled)
            {
                Logger.WriteLine("Auto scan: aborting upkeep mode (disabled)");
                timerAutoScanUpkeep.Stop();
            }
        }

        private void buttonCapture_Paint(object sender, PaintEventArgs e)
        {
            if (bCanDrawCaptureMarker)
            {
                Pen markerPen = new Pen(SystemColors.HotTrack, 2);
                markerPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                markerPen.DashOffset = dashAnimOffset;
                int drawOffset = 6;

                e.Graphics.DrawRectangle(markerPen,
                    e.ClipRectangle.X + drawOffset, e.ClipRectangle.Y + drawOffset,
                    e.ClipRectangle.Width - (2 * drawOffset), e.ClipRectangle.Height - (2 * drawOffset));
            }
        }

        private void timerDashAnim_Tick(object sender, EventArgs e)
        {
            dashAnimOffset = (dashAnimOffset + 1) % 32;
            buttonCapture.Invalidate();
        }

        private void timerHideSwapWarning_Tick(object sender, EventArgs e)
        {
            panelSwapWarning.Visible = false;
        }

        private void timerAutoScanUpkeep_Tick(object sender, EventArgs e)
        {
            Logger.WriteLine("Auto scan: upkeep mode timed out");
            timerAutoScanUpkeep.Stop();
            timerTurnScan.Stop();
            bCanAutoCapture = false;
            UpdateAutoCaptureMarker();
        }

        private void ShowCactpotLine(Rectangle fromBox, Rectangle toBox)
        {
            Point fromMidPt = new Point((fromBox.Left + fromBox.Right) / 2, (fromBox.Top + fromBox.Bottom) / 2);
            Point toMidPt = new Point((toBox.Left + toBox.Right) / 2, (toBox.Top + toBox.Bottom) / 2);
            float lineLen = (float)Math.Sqrt(((toMidPt.X - fromMidPt.X) * (toMidPt.X - fromMidPt.X)) + ((toMidPt.Y - fromMidPt.Y) * (toMidPt.Y - fromMidPt.Y)));
            Point lineOffset = new Point((int)((fromBox.Width / 2) * (toMidPt.X - fromMidPt.X) / lineLen), (int)((fromBox.Width / 2) * (toMidPt.Y - fromMidPt.Y) / lineLen));

            int offset = 10;
            panelMarkerLine.Bounds = new Rectangle(
                Math.Min(fromMidPt.X, toMidPt.X) - offset,
                Math.Min(fromMidPt.Y, toMidPt.Y) - offset,
                Math.Abs(fromMidPt.X - toMidPt.X) + (offset * 2),
                Math.Abs(fromMidPt.Y - toMidPt.Y) + (offset * 2));

            cactpotLinePt0 = new Point(fromMidPt.X - panelMarkerLine.Location.X - lineOffset.X, fromMidPt.Y - panelMarkerLine.Location.Y - lineOffset.Y);
            cactpotLinePt1 = new Point(toMidPt.X - panelMarkerLine.Location.X + lineOffset.X, toMidPt.Y - panelMarkerLine.Location.Y + lineOffset.Y);

            panelMarkerLine.Visible = true;
            panelMarkerLine.Invalidate();
        }

        private void panelMarkerLine_Paint(object sender, PaintEventArgs e)
        {
            Pen linePen = new Pen(Color.Lime, 5);
            e.Graphics.DrawLine(linePen, cactpotLinePt0, cactpotLinePt1);
        }

        private void checkBoxFullScreenScan_CheckedChanged(object sender, EventArgs e)
        {
            PlayerSettingsDB.Get().useFullScreenCapture = checkBoxFullScreenScan.Checked;
            UpdateScreenState();
            OnUpdateState.Invoke();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // turn on WS_EX_TOOLWINDOW style bit
                cp.ExStyle |= 0x80;
                return cp;
            }
        }

        // XInput enable
        public void SetXInputEnble(bool enable)
        {
            if (enable)
            {
                XInputStub.OnEventMotionTrigger += XInputEventMotion;
            }
            else
            {
                XInputStub.OnEventMotionTrigger -= XInputEventMotion;
            }
        }

        // XInput delegate
        public void XInputEventMotion()
        {
            //System.Diagnostics.Debug.WriteLine("Invoke! " +  DateTime.Now.ToLongTimeString());

            if (!InvokeRequired)
                buttonCaptureWoker();
            else
            {
                Invoke(new MethodInvoker(delegate ()
                {
                    buttonCaptureWoker();
                }));
            }
        }

    }
}
