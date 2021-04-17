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
        public ScreenshotAnalyzer screenReader;
        private TriadNpc npc;

        public delegate void UpdateStateDelegate();
        public event UpdateStateDelegate OnUpdateState;

        public FormOverlay()
        {
            InitializeComponent();

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

        public void UpdateScreenState(ScreenshotAnalyzer screenReader, bool bDebugMode = false)
        {
            checkBoxDetails_CheckedChanged(null, null);

            if (screenReader.GetCurrentState() != ScreenshotAnalyzer.EState.NoErrors)
            {
                Logger.WriteLine("Capture failed: " + screenReader.GetCurrentState());
                bHasValidMarkerDeck = false;
                bHasValidMarkerBoard = false;
                UpdateStatusDescription();
                return;
            }

            scanId++;
            labelScanId.Text = "Scan Id: " + scanId;
            Logger.WriteLine("Capture " + labelScanId.Text);

            // multi monitor setup: make sure that overlay and game and on the same monitor
            Rectangle gameWindowRect = screenReader.GetGameWindowRect();
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

            TriadGameScreenMemory.EUpdateFlags updateFlags = TriadGameScreenMemory.EUpdateFlags.None;
            if (screenReader.GetCurrentGameType() == ScreenshotAnalyzer.EGame.TripleTriad)
            {
                // update overlay locations
                if (bCanAdjustSummaryLocation)
                {
                    Rectangle gridRect = screenReader.GetGridRect();
                    if ((gridRect.Width > 0) && (gameWindowRect.Width > 0))
                    {
                        bCanAdjustSummaryLocation = false;
                        screenReader.ConvertToScaledScreen(ref gridRect);

                        UpdateOverlayLocation(gameWindowRect.Left + ((gridRect.Left + gridRect.Right) / 2) - (panelSummary.Width / 2) - Location.X, gameWindowRect.Top + gridRect.Bottom + 50 - Location.Y);
                    }
                }

                // solver logic
                updateFlags = screenMemory.OnNewScan(screenReader.currentTriadGame, npc);
                if (updateFlags != TriadGameScreenMemory.EUpdateFlags.None)
                {
                    int markerDeckPos = -1;
                    int markerBoardPos = -1;

                    FindNextMove(out markerDeckPos, out markerBoardPos, out TriadGameResultChance bestChance);
                    ETriadGameState expectedResult = bestChance.expectedResult;

                    TriadCard suggestedCard = screenMemory.deckBlue.GetCard(markerDeckPos);
                    Logger.WriteLine("  suggested move: [" + markerBoardPos + "] " + ETriadCardOwner.Blue + " " + (suggestedCard != null ? suggestedCard.Name : "??") + " (expected: " + expectedResult + ")");

                    bHasValidMarkerDeck = false;
                    bHasValidMarkerBoard = false;
                    if (markerDeckPos >= 0 && markerBoardPos >= 0)
                    {
                        try
                        {
                            Rectangle rectDeckPos = screenReader.GetBlueCardRect(markerDeckPos);
                            Rectangle rectBoardPos = screenReader.GetBoardCardRect(markerBoardPos);
                            rectDeckPos.Offset(gameWindowRect.Location.X - Location.X, gameWindowRect.Location.Y - Location.Y);
                            rectBoardPos.Offset(gameWindowRect.Location.X - Location.X, gameWindowRect.Location.Y - Location.Y);

                            screenReader.ConvertToScaledScreen(ref rectDeckPos);
                            screenReader.ConvertToScaledScreen(ref rectBoardPos);
                            rectDeckPos.Inflate(10, 10);
                            rectBoardPos.Inflate(10, 10);

                            panelMarkerDeck.Bounds = rectDeckPos;
                            panelMarkerBoard.Bounds = rectBoardPos;
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
            else if (screenReader.GetCurrentGameType() == ScreenshotAnalyzer.EGame.MiniCactpot)
            {
                // update overlay locations
                if (bCanAdjustSummaryLocation)
                {
                    Rectangle boardRect = screenReader.GetCactpotBoardRect();
                    if ((boardRect.Width > 0) && (gameWindowRect.Width > 0))
                    {
                        screenReader.ConvertToScaledScreen(ref boardRect);

                        bCanAdjustSummaryLocation = false;
                        UpdateOverlayLocation(gameWindowRect.Left + ((boardRect.Left + boardRect.Right) / 2) - (panelSummary.Width / 2) - Location.X, gameWindowRect.Top + boardRect.Bottom + 50 - Location.Y);
                    }
                }

                // solver logic
                if (screenReader.currentCactpotGame.numRevealed > 3)
                {
                    bHasValidMarkerBoard = false;

                    CactpotGame.FindBestLine(screenReader.currentCactpotGame.board, out int fromIdx, out int toIdx);
                    Logger.WriteLine("  suggested line: [" + fromIdx + "] -> [" + toIdx + "]");

                    if (fromIdx >= 0 && toIdx >= 0)
                    {
                        Rectangle fromBox = screenReader.GetCactpotCircleBox(fromIdx);
                        Rectangle toBox = screenReader.GetCactpotCircleBox(toIdx);
                        fromBox.Offset(gameWindowRect.Location.X - Location.X, gameWindowRect.Location.Y - Location.Y);
                        toBox.Offset(gameWindowRect.Location.X - Location.X, gameWindowRect.Location.Y - Location.Y);
                        screenReader.ConvertToScaledScreen(ref fromBox);
                        screenReader.ConvertToScaledScreen(ref toBox);
                        fromBox.Inflate(10, 10);
                        toBox.Inflate(10, 10);

                        ShowCactpotLine(fromBox, toBox);
                    }
                }
                else
                {
                    int markerPos = CactpotGame.FindNextCircle(screenReader.currentCactpotGame.board);
                    Logger.WriteLine("  suggested move: [" + markerPos + "]");

                    bHasValidMarkerBoard = (markerPos >= 0);
                    if (bHasValidMarkerBoard)
                    {
                        Rectangle rectBoardPos = screenReader.GetCactpotCircleBox(markerPos);
                        rectBoardPos.Offset(gameWindowRect.Location.X - Location.X, gameWindowRect.Location.Y - Location.Y);
                        screenReader.ConvertToScaledScreen(ref rectBoardPos);
                        rectBoardPos.Inflate(10, 10);

                        panelMarkerBoard.Bounds = rectBoardPos;
                        panelMarkerBoard.BackColor = Color.Lime;
                    }
                }

                for (int Idx = 0; Idx < screenReader.currentCactpotGame.board.Length; Idx++)
                {
                    int numInCircle = screenReader.currentCactpotGame.board[Idx];
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

                labelRules.Text = "Rules: " + ((desc.Length > 2) ? desc.Remove(desc.Length - 2, 2) : "unknown");
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.BlueDeck) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                deckCtrlBlue.SetDeck(screenReader.currentTriadGame.blueDeck);
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.RedDeck) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                deckCtrlRed.SetDeck(screenReader.currentTriadGame.redDeck);
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
                Rectangle ruleRect = screenReader.GetRuleBoxRect();
                if (gameWindowRect.Width > 0 && ruleRect.Width > 0)
                {
                    Rectangle warningBounds = new Rectangle(ruleRect.Left, ruleRect.Top - panelSwapWarning.Height - 10, 0, 0);
                    warningBounds.Offset(gameWindowRect.Location);
                    screenReader.ConvertToScaledScreen(ref warningBounds);

                    panelSwapWarning.Location = warningBounds.Location;
                    panelSwapWarning.Visible = true;
                    timerHideSwapWarning.Stop();
                    timerHideSwapWarning.Start();
                }
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.SwapHints) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                Rectangle rectDeckPos = screenReader.GetBlueCardRect(screenMemory.swappedBlueCardIdx);
                rectDeckPos.Offset(gameWindowRect.Location.X - Location.X, gameWindowRect.Location.Y - Location.Y);
                screenReader.ConvertToScaledScreen(ref rectDeckPos);
                rectDeckPos.Inflate(-10, -10);

                panelMarkerSwap.Bounds = rectDeckPos;
                panelMarkerSwap.Visible = true;
            }

            bCanStopTurnScan = false;
            bCanAutoCapture = false;

            timerFadeMarkers.Enabled = bHasValidMarkerDeck || bHasValidMarkerBoard || panelMarkerSwap.Visible || panelMarkerLine.Visible;
            panelMarkerDeck.Visible = bHasValidMarkerDeck;
            panelMarkerBoard.Visible = bHasValidMarkerBoard;

            timerTurnScan.Enabled = true;
            timerTurnScan_Tick(null, null);
        }

        private void FindNextMove(out int blueCardIdx, out int boardCardIdx, out TriadGameResultChance bestChance)
        {
            blueCardIdx = -1;
            boardCardIdx = -1;

            screenMemory.gameSession.SolverFindBestMove(screenMemory.gameState, out int solverBoardPos, out TriadCard solverTriadCard, out bestChance);

            blueCardIdx = screenMemory.deckBlue.GetCardIndex(solverTriadCard);
            if (blueCardIdx >= 0)
            {
                boardCardIdx = solverBoardPos;
            }
        }

        private void buttonCaptureWoker()
        {
            screenReader.DoWork(ScreenshotAnalyzer.EMode.All);
            UpdateScreenState(screenReader);
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
            bool bShowTriadControls = (screenReader == null || screenReader.GetCurrentGameType() == ScreenshotAnalyzer.EGame.TripleTriad);

            panelDetails.Visible = checkBoxDetails.Checked && bShowTriadControls;
            panelBoard.Visible = checkBoxDetails.Checked && bShowTriadControls;
            panelCactpot.Visible = checkBoxDetails.Checked && !bShowTriadControls;

            // keep panelScanResolution disabled
            // panelScanResolution.Visible = checkBoxDetails.Checked;
        }

        public bool IsUsingAutoScan()
        {
            bool bIsAutoScanAllowed = (screenReader == null || screenReader.GetCurrentGameType() == ScreenshotAnalyzer.EGame.TripleTriad);
            return checkBoxAutoScan.Checked && bIsAutoScanAllowed;
        }

        public void SetNpc(TriadNpc inNpc)
        {
            npc = inNpc;

            UpdateStatusDescription();
            labelNpc.Text = "NPC: " + ((npc != null) ? npc.ToString() : "unknown");
            labelRules.Text = "Rules: npc changed, waiting for scan...";
        }

        public void UpdateOverlayLocation(int screenX, int screenY)
        {
            screenX = Math.Min(Math.Max(screenX, 0), Size.Width - panelSummary.Width);
            screenY = Math.Min(Math.Max(screenY, 0), Size.Height - panelSummary.Height);

            panelSummary.Location = new Point(screenX, screenY);

            panelDetails.Location = new Point(screenX - panelDetails.Width - 10, screenY);
            panelBoard.Location = new Point(screenX + panelSummary.Width + 10, screenY);
            panelCactpot.Location = new Point(screenX + panelSummary.Width + 10, screenY);
            panelDebug.Location = new Point(screenX, screenY + panelSummary.Height + 10);
            panelScanResolution.Location = new Point(screenX, screenY + panelSummary.Height + 10);
        }

        public void InitOverlayLocation(Rectangle mainWindowBounds)
        {
            // multi monitor setup: make sure that overlay and game and on the same monitor
            Rectangle gameWindowBounds = mainWindowBounds;
            if (screenReader != null)
            {
                Rectangle testBounds = screenReader.FindGameWindowBounds();
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
            ScreenshotAnalyzer.EState showState = (screenReader != null) ? screenReader.GetCurrentState() : ScreenshotAnalyzer.EState.NoErrors;
            ScreenshotAnalyzer.ETurnState timerState = (screenReader != null) ? screenReader.GetCurrentTurnState() : ScreenshotAnalyzer.ETurnState.MissingTimer;
            ScreenshotAnalyzer.EGame showGame = (screenReader != null) ? screenReader.GetCurrentGameType() : ScreenshotAnalyzer.EGame.TripleTriad;

            switch (showState)
            {
                case ScreenshotAnalyzer.EState.MissingGameProcess: SetStatusText("Game is not running", SystemIcons.Error); break;
                case ScreenshotAnalyzer.EState.MissingGameWindow: SetStatusText("Can't find game window", SystemIcons.Error); break;
                case ScreenshotAnalyzer.EState.MissingGrid: SetStatusText("Can't find board", SystemIcons.Error); break;
                case ScreenshotAnalyzer.EState.MissingCards: SetStatusText("Can't find blue deck", SystemIcons.Error); break;
                case ScreenshotAnalyzer.EState.FailedCardMatching: SetStatusText("Unknown cards! Check Play:Screenshot for details", SystemIcons.Warning); break;
                case ScreenshotAnalyzer.EState.UnknownHash: SetStatusText("Unknown pattern! Check Play:Screenshot for details", SystemIcons.Warning); break;
                default:
                    switch (showGame)
                    {
                        case ScreenshotAnalyzer.EGame.TripleTriad:
                            string npcDesc = (npc != null) ? (npc.Name + ": ") : "";
                            switch (timerState)
                            {
                                case ScreenshotAnalyzer.ETurnState.MissingTimer: SetStatusText(npcDesc + "Ready", SystemIcons.Information); break;
                                case ScreenshotAnalyzer.ETurnState.Waiting: SetStatusText(npcDesc + "Waiting for blue turn", SystemIcons.Shield); break;
                                case ScreenshotAnalyzer.ETurnState.Active:
                                    bool bIsMouseOverGrid = (screenReader != null) && screenReader.IsInScanArea(Cursor.Position);
                                    if (bIsMouseOverGrid && IsUsingAutoScan())
                                    {
                                        SetStatusText("Move cursor away from scan zone!", SystemIcons.Warning);
                                    }
                                    else
                                    {
                                        SetStatusText(npcDesc + "Ready (active turn!)", SystemIcons.Information);
                                    }
                                    break;
                            }
                            break;

                        case ScreenshotAnalyzer.EGame.MiniCactpot: SetStatusText("Mini cactpot: Ready", SystemIcons.Information); break;
                        default: break;
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

            if ((screenReader == null) ||
                (bCanStopTurnScan && screenReader.GetCurrentTurnState() == ScreenshotAnalyzer.ETurnState.MissingTimer))
            {
                timerTurnScan.Stop();
                bCanAutoCapture = false;
            }
            else
            {
                screenReader.DoWork(ScreenshotAnalyzer.EMode.TurnTimerOnly | (bDebugMode ? ScreenshotAnalyzer.EMode.DebugTimerDetails : ScreenshotAnalyzer.EMode.None));
                bCanStopTurnScan = true;
            }

            UpdateStatusDescription();
            if (bDebugMode) { UpdateDebugDetails(); }

            if (screenReader != null && IsUsingAutoScan())
            {
                if (screenReader.GetCurrentTurnState() == ScreenshotAnalyzer.ETurnState.Waiting)
                {
                    bCanAutoCapture = true;
                }
                else if (screenReader.GetCurrentTurnState() == ScreenshotAnalyzer.ETurnState.Active)
                {
                    bool bIsMouseOverGrid = screenReader.IsInScanArea(Cursor.Position);
                    if (bCanAutoCapture && !bIsMouseOverGrid)
                    {
                        bCanAutoCapture = false;
                        buttonCapture_Click(null, null);
                    }
                }
            }
        }

        private void UpdateDebugDetails()
        {
#if DEBUG
            if (screenReader != null)
            {
                panelDebug.Visible = true;
                labelDebugTime.Text = DateTime.Now.ToString("HH:mm:ss.ff");
                labelDebugDesc.Text = "Status: " + screenReader.GetCurrentTurnState();
                pictureDebugScreen.Image = screenReader.GetDebugScreenshot();
            }
#endif // DEBUG
        }

        private void UpdateRedDeckDetails(TriadDeckInstanceScreen deck)
        {
            string NumPlacedPrefix = "All placed: ";
            string VarPlacedPrefix = "Var placed: ";

            if (deck == null || deck.deck == null)
            {
                for (int Idx = 0; Idx < redDeckKnownCards.Length; Idx++)
                {
                    redDeckKnownCards[Idx].Visible = false;
                    redDeckUnknownCards[Idx].Visible = false;
                    deckCtrlRed.SetTransparent(Idx, false);
                }

                labelNumPlaced.Text = NumPlacedPrefix + "0";
                labelUnknownPlaced.Text = VarPlacedPrefix + "0";
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

                labelNumPlaced.Text = NumPlacedPrefix + deck.numPlaced;
                labelUnknownPlaced.Text = VarPlacedPrefix + deck.numUnknownPlaced;
            }
        }

        private void UpdateAutoCaptureMarker()
        {
            bool bCanShow = IsUsingAutoScan() && (screenReader != null) && (screenReader.GetCurrentTurnState() != ScreenshotAnalyzer.ETurnState.MissingTimer);
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
            UpdateScreenState(screenReader);
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
