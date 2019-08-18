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
        private bool bHasValidMarkers;
        private bool bCanDrawCaptureMarker;
        private bool bCanAdjustSummaryLocation;
        private bool bCanStopTurnScan;
        private bool bCanAutoCapture;
        private int dashAnimOffset;
        private Point summaryMovePt;

        private TriadGameSession gameSession;
        private TriadGameData gameState;
        private TriadDeckInstanceScreen blueDeck;
        private TriadDeckInstanceScreen redDeck;
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

            panelMarkerBoard.Visible = false;
            panelMarkerDeck.Visible = false;
            panelDetails.Visible = false;
            panelBoard.Visible = false;
            panelDebug.Visible = false;
            labelStatus.Focus();

            bHasValidMarkers = false;
            bCanAdjustSummaryLocation = false;
            bCanStopTurnScan = true;
            bCanAutoCapture = false;
            dashAnimOffset = 0;

            UpdateOverlayLocation(10, 10);
            UpdateAutoCaptureMarker();
            UpdateStatusDescription();

            gameSession = new TriadGameSession();
            gameState = new TriadGameData();
            blueDeck = new TriadDeckInstanceScreen();
            redDeck = new TriadDeckInstanceScreen();
        }

        public void InitializeAssets(ImageList cardImageList)
        {
            cardImages = cardImageList;

            for (int Idx = 0; Idx < boardControls.Length; Idx++)
            {
                boardControls[Idx].cardIcons = cardImages;
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

        public void UpdateScreenState(ScreenshotAnalyzer screenReader, bool bDebugMode = false)
        {
            if (screenReader.GetCurrentState() != ScreenshotAnalyzer.EState.NoErrors)
            {
                Logger.WriteLine("Capture failed: " + screenReader.GetCurrentState());
                bHasValidMarkers = false;
                UpdateStatusDescription();
                return;
            }

            bool bModsChanged = (gameSession.modifiers.Count != screenReader.currentGame.mods.Count) || !gameSession.modifiers.All(screenReader.currentGame.mods.Contains);
            if (bModsChanged)
            {
                gameSession.modifiers = screenReader.currentGame.mods;
                gameSession.specialRules = ETriadGameSpecialMod.None;

                string desc = "";
                foreach (TriadGameModifier mod in gameSession.modifiers)
                {
                    desc += mod.ToString() + ", ";
                }

                labelRules.Text = "Rules: " + ((desc.Length > 2) ? desc.Remove(desc.Length - 2, 2) : "unknown");
            }

            gameSession.forcedBlueCard = screenReader.currentGame.forcedBlueCard;
    
            gameState.numCardsPlaced = 0;
            gameState.state = ETriadGameState.InProgressBlue;

            bool bBoardChanged = false;
            for (int Idx = 0; Idx < gameState.board.Length; Idx++)
            {
                bool bWasNull = gameState.board[Idx] == null;
                bool bIsNull = screenReader.currentGame.board[Idx] == null;

                if (bWasNull && !bIsNull)
                {
                    bBoardChanged = true;
                    gameState.board[Idx] = new TriadCardInstance(screenReader.currentGame.board[Idx], screenReader.currentGame.boardOwner[Idx]);
                }
                else if (!bWasNull && bIsNull)
                {
                    bBoardChanged = true;
                    gameState.board[Idx] = null;
                }
                else if (!bWasNull && !bIsNull)
                {
                    if (gameState.board[Idx].owner != screenReader.currentGame.boardOwner[Idx] ||
                        gameState.board[Idx].card != screenReader.currentGame.board[Idx])
                    {
                        bBoardChanged = true;
                        gameState.board[Idx] = new TriadCardInstance(screenReader.currentGame.board[Idx], screenReader.currentGame.boardOwner[Idx]);
                    }
                }

                gameState.numCardsPlaced += (gameState.board[Idx] != null) ? 1 : 0;
                boardControls[Idx].SetCard(gameState.board[Idx]);
            }

            if (bBoardChanged)
            {
                foreach (TriadGameModifier mod in gameSession.modifiers)
                {
                    mod.OnScreenUpdate(gameState);
                }
            }

            bool bBlueDeckChanged = !deckCtrlBlue.IsMatching(screenReader.currentGame.blueDeck);
            if (bBlueDeckChanged)
            {
                blueDeck.cards = screenReader.currentGame.blueDeck;
                blueDeck.UpdateAvailableCards();
                deckCtrlBlue.SetDeck(screenReader.currentGame.blueDeck);
            }

            bool bRedDeckChanged = !deckCtrlBlue.IsMatching(screenReader.currentGame.redDeck) || (redDeck.npcDeck != npc.Deck);
            if (bRedDeckChanged)
            {
                redDeck.cards = screenReader.currentGame.redDeck;
                redDeck.npcDeck = npc.Deck;
                redDeck.UpdateAvailableCards();
                deckCtrlRed.SetDeck(screenReader.currentGame.redDeck);
            }

            bool bAnythingChanged = bBoardChanged || bBlueDeckChanged || bRedDeckChanged || bModsChanged;
            Logger.WriteLine("UpdateScreenState> board:" + (bBoardChanged ? "changed" : "same") +
                ", blue:" + (bBlueDeckChanged ? "changed" : "same") +
                ", red:" + (bRedDeckChanged ? "changed" : "same") +
                ", mods:" + (bModsChanged ? "changed" : "same") +
                ", state:" + screenReader.GetCurrentState() +
                " => " + (bAnythingChanged ? "UPDATE" : "skip"));

            int markerDeckPos = -1;
            int markerBoardPos = -1;
            ETriadGameState expectedResult = ETriadGameState.BlueWins;

            if (bAnythingChanged)
            {
                FindNextMove(out markerDeckPos, out markerBoardPos, out TriadGameResultChance bestChance);
                expectedResult = bestChance.expectedResult;
            }

            // update overlay locations
            Rectangle gameWindowRect = screenReader.GetGameWindowRect();            
            if (gameWindowRect.Width > 0)
            {
                Rectangle gridRect = screenReader.GetGridRect();

                if (gridRect.Width > 0)
                {
                    // one time only, give user ability to move it if needed
                    if (bCanAdjustSummaryLocation)
                    {
                        bCanAdjustSummaryLocation = false;
                        UpdateOverlayLocation(gameWindowRect.Left + ((gridRect.Left + gridRect.Right) / 2) - (panelSummary.Width / 2), gameWindowRect.Top + gridRect.Bottom + 50);
                    }

                    if (bAnythingChanged)
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

            bCanStopTurnScan = false;
            bCanAutoCapture = false;

            timerFadeMarkers.Enabled = bHasValidMarkers;
            panelMarkerDeck.Visible = bHasValidMarkers;
            panelMarkerBoard.Visible = bHasValidMarkers;

            timerTurnScan.Enabled = true;
            timerTurnScan_Tick(null, null);
        }

        private void FindNextMove(out int blueCardIdx, out int boardCardIdx, out TriadGameResultChance bestChance)
        {
            blueCardIdx = -1;
            boardCardIdx = -1;

            gameState.deckBlue = blueDeck;
            gameState.deckRed = redDeck;
            gameSession.SolverFindBestMove(gameState, out int solverBoardPos, out TriadCard solverTriadCard, out bestChance);

            if (solverTriadCard != null)
            {
                for (int Idx = 0; Idx < blueDeck.cards.Length; Idx++)
                {
                    if (blueDeck.cards[Idx] == solverTriadCard)
                    {
                        boardCardIdx = solverBoardPos;
                        blueCardIdx = Idx;
                        break;
                    }
                }
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
            panelSummary.Location = new Point(screenX, screenY);

            panelDetails.Location = new Point(screenX - panelDetails.Width - 10, screenY);
            panelBoard.Location = new Point(screenX + panelSummary.Width + 10, screenY);
            panelDebug.Location = new Point(screenX, screenY + panelSummary.Height + 10);
        }

        public void InitOverlayLocation()
        {
            panelSummary.Left = (Width - panelSummary.Width) / 2;
            panelSummary.Top = Bottom - panelSummary.Height - 10;

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
    }
}
