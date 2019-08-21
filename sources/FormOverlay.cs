using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FFTriadBuddy
{
    public partial class FormOverlay : Form
    {
        private ImageList cardImages;
        private CardCtrl[] boardControls;
        private CardCtrl[] redDeckKnownCards;
        private CardCtrl[] redDeckUnknownCards;
        private bool bHasValidMarkers;
        private bool bCanDrawCaptureMarker;
        private bool bCanAdjustSummaryLocation;
        private bool bCanStopTurnScan;
        private bool bCanAutoCapture;
        private int dashAnimOffset;
        private int scanId;
        private Point summaryMovePt;

        private TriadGameScreenMemory screenMemory;
        public ScreenshotAnalyzer screenReader;
        private TriadNpc npc;

        public delegate void UpdateStateDelegate();
        public event UpdateStateDelegate OnUpdateState;

        public FormOverlay()
        {
            InitializeComponent();

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
            panelDetails.Visible = false;
            panelBoard.Visible = false;
            panelDebug.Visible = false;
            panelSwapWarning.Visible = false;
            labelStatus.Focus();
            labelSwapWarningIcon.Image = SystemIcons.Warning.ToBitmap();

            bHasValidMarkers = false;
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
            if (screenReader.GetCurrentState() != ScreenshotAnalyzer.EState.NoErrors)
            {
                Logger.WriteLine("Capture failed: " + screenReader.GetCurrentState());
                bHasValidMarkers = false;
                UpdateStatusDescription();
                return;
            }

            scanId++;
            labelScanId.Text = "Scan Id: " + scanId;
            Logger.WriteLine("Capture " + labelScanId.Text);

            TriadGameScreenMemory.EUpdateFlags updateFlags = screenMemory.OnNewScan(screenReader.currentGame, npc);

            int markerDeckPos = -1;
            int markerBoardPos = -1;
            ETriadGameState expectedResult = ETriadGameState.BlueWins;

            bool bNeedsHintUpdate = (updateFlags != TriadGameScreenMemory.EUpdateFlags.None);
            if (bNeedsHintUpdate)
            {
                FindNextMove(out markerDeckPos, out markerBoardPos, out TriadGameResultChance bestChance);
                expectedResult = bestChance.expectedResult;

                TriadCard suggestedCard = screenMemory.deckBlue.GetCard(markerDeckPos);
                Logger.WriteLine("  suggested move: [" + markerBoardPos + "] " + ETriadCardOwner.Blue + " " + (suggestedCard != null ? suggestedCard.Name : "??") + " (expected: " + expectedResult + ")");
            }

            // update overlay locations
            Rectangle gameWindowRect = screenReader.GetGameWindowRect();            
            if (gameWindowRect.Width > 0)
            {
                Rectangle gridRect = screenReader.GetGridRect();
                if (gridRect.Width > 0)
                {
                    // multi monitor setup: make sure that overlay and game and on the same monitor
                    Rectangle gameScreenBounds = Screen.GetBounds(gameWindowRect);
                    Point centerPt = new Point((Left + Right) / 2, (Top + Bottom) / 2);
                    if (!gameScreenBounds.Contains(centerPt))
                    {
                        Location = gameScreenBounds.Location;
                        Size = gameScreenBounds.Size;
                        bCanAdjustSummaryLocation = true;
                    }

                    // one time only, give user ability to move it if needed
                    if (bCanAdjustSummaryLocation)
                    {
                        bCanAdjustSummaryLocation = false;
                        UpdateOverlayLocation(gameWindowRect.Left + ((gridRect.Left + gridRect.Right) / 2) - (panelSummary.Width / 2), gameWindowRect.Top + gridRect.Bottom + 50);
                    }

                    if (bNeedsHintUpdate)
                    {
                        bHasValidMarkers = false;
                        if (markerDeckPos >= 0 && markerBoardPos >= 0)
                        {
                            try
                            {
                                Rectangle rectDeckPos = screenReader.GetBlueCardRect(markerDeckPos);
                                Rectangle rectBoardPos = screenReader.GetBoardCardRect(markerBoardPos);
                                rectDeckPos.Inflate(10, 10);
                                rectBoardPos.Inflate(10, 10);

                                panelMarkerDeck.Top = rectDeckPos.Top + gameWindowRect.Top;
                                panelMarkerDeck.Left = rectDeckPos.Left + gameWindowRect.Left;
                                panelMarkerDeck.Width = rectDeckPos.Width;
                                panelMarkerDeck.Height = rectDeckPos.Height;

                                panelMarkerBoard.Top = rectBoardPos.Top + gameWindowRect.Top;
                                panelMarkerBoard.Left = rectBoardPos.Left + gameWindowRect.Left;
                                panelMarkerBoard.Width = rectBoardPos.Width;
                                panelMarkerBoard.Height = rectBoardPos.Height;
                                panelMarkerBoard.BackColor =
                                    (expectedResult == ETriadGameState.BlueWins) ? Color.Lime :
                                    (expectedResult == ETriadGameState.BlueDraw) ? Color.Gold :
                                    Color.Red;

                                bHasValidMarkers = true;
                            }
                            catch (Exception) { }
                        }
                    }
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
                deckCtrlBlue.SetDeck(screenReader.currentGame.blueDeck);
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.RedDeck) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                deckCtrlRed.SetDeck(screenReader.currentGame.redDeck);
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
                    panelSwapWarning.Top = gameWindowRect.Top + ruleRect.Top - panelSwapWarning.Height - 10;
                    panelSwapWarning.Left = gameWindowRect.Left + ruleRect.Left;
                    panelSwapWarning.Visible = true;
                    timerHideSwapWarning.Stop();
                    timerHideSwapWarning.Start();
                }
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.SwapHints) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                Rectangle rectDeckPos = screenReader.GetBlueCardRect(screenMemory.swappedBlueCardIdx);
                rectDeckPos.Inflate(-10, -10);

                panelMarkerSwap.Top = rectDeckPos.Top + gameWindowRect.Top;
                panelMarkerSwap.Left = rectDeckPos.Left + gameWindowRect.Left;
                panelMarkerSwap.Width = rectDeckPos.Width;
                panelMarkerSwap.Height = rectDeckPos.Height;
                panelMarkerSwap.Visible = true;
            }

            bCanStopTurnScan = false;
            bCanAutoCapture = false;

            timerFadeMarkers.Enabled = bHasValidMarkers || panelMarkerSwap.Visible;
            panelMarkerDeck.Visible = bHasValidMarkers;
            panelMarkerBoard.Visible = bHasValidMarkers;

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

        private void buttonCapture_Click(object sender, EventArgs e)
        {
            screenReader.DoWork(ScreenshotAnalyzer.EMode.All);
            UpdateScreenState(screenReader);
            OnUpdateState.Invoke();
        }

        private void timerFadeMarkers_Tick(object sender, EventArgs e)
        {
            panelMarkerDeck.Visible = false;
            panelMarkerBoard.Visible = false;
            panelMarkerSwap.Visible = false;
            timerFadeMarkers.Enabled = false;
        }

        private void checkBoxDetails_CheckedChanged(object sender, EventArgs e)
        {
            panelDetails.Visible = checkBoxDetails.Checked;
            panelBoard.Visible = checkBoxDetails.Checked;
        }

        public bool IsUsingAutoScan()
        {
            return checkBoxAutoScan.Checked;
        }

        public void SetNpc(TriadNpc inNpc)
        {
            npc = inNpc;

            UpdateStatusDescription();
            labelNpc.Text = "NPC: " + ((npc != null) ? npc.ToString() : "unknown");
        }

        public void UpdateOverlayLocation(int screenX, int screenY)
        {
            screenX = Math.Min(Math.Max(screenX, 0), Size.Width - panelSummary.Width);
            screenY = Math.Min(Math.Max(screenY, 0), Size.Height - panelSummary.Height);

            panelSummary.Location = new Point(screenX, screenY);

            panelDetails.Location = new Point(screenX - panelDetails.Width - 10, screenY);
            panelBoard.Location = new Point(screenX + panelSummary.Width + 10, screenY);
            panelDebug.Location = new Point(screenX, screenY + panelSummary.Height + 10);
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

            switch (showState)
            {
                case ScreenshotAnalyzer.EState.MissingGameProcess: SetStatusText("Game is not running", SystemIcons.Error); break;
                case ScreenshotAnalyzer.EState.MissingGameWindow: SetStatusText("Can't find game window", SystemIcons.Error); break;
                case ScreenshotAnalyzer.EState.MissingGrid: SetStatusText("Can't find board", SystemIcons.Error); break;
                case ScreenshotAnalyzer.EState.MissingCards: SetStatusText("Can't find blue deck", SystemIcons.Error); break;
                case ScreenshotAnalyzer.EState.FailedCardMatching: SetStatusText("Unknown cards! Check Play:Screenshot for details", SystemIcons.Warning); break;
                case ScreenshotAnalyzer.EState.UnknownHash: SetStatusText("Unknown pattern! Check Play:Screenshot for details", SystemIcons.Warning); break;
                default:
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
    }
}
