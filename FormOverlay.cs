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

        private TriadGameSession gameSession;
        private TriadGameData gameState;
        private TriadDeckInstanceScreen blueDeck;
        private TriadDeckInstanceScreen redDeck;
        public ScreenshotAnalyzer screenReader;
        public TriadNpc npc;

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
            bHasValidMarkers = false;

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
        }
        /*
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == HitInvisConst.WM_NCHITTEST)
                m.Result = (IntPtr)HitInvisConst.HTTRANSPARENT;
            else
                base.WndProc(ref m);
        }*/

        public void UpdateScreenState(ScreenshotAnalyzer screenReader, bool bDebugMode = false)
        {
            if (screenReader.GetCurrentState() != ScreenshotAnalyzer.EState.NoErrors)
            {
                Logger.WriteLine("Capture failed: " + screenReader.GetCurrentState());
                bHasValidMarkers = false;
                button1.Text = "Failed! Check details in screenshot tab";
                return;
            }

            button1.Text = "Capture";

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

            labelNpc.Text = "NPC: " + ((npc != null) ? npc.ToString() : "unknown");
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
                deckCtrlBlue.SetDeck(screenReader.currentGame.blueDeck);
            }

            bool bRedDeckChanged = !deckCtrlBlue.IsMatching(screenReader.currentGame.redDeck) || (redDeck.npcDeck != npc.Deck);
            if (bRedDeckChanged)
            {
                redDeck.cards = screenReader.currentGame.redDeck;
                redDeck.npcDeck = npc.Deck;
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
                Top = gameWindowRect.Top;
                Left = gameWindowRect.Left;
                Width = gameWindowRect.Width + 100;
                Height = gameWindowRect.Height + 100;

                Rectangle gridRect = screenReader.GetGridRect();
                if (gridRect.Width > 0)
                {
                    panelSummary.Top = gridRect.Bottom + 50;
                    panelSummary.Left = ((gridRect.Left + gridRect.Right) / 2) - (panelSummary.Width / 2);

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

                                panelMarkerDeck.Top = rectDeckPos.Top;
                                panelMarkerDeck.Left = rectDeckPos.Left;
                                panelMarkerDeck.Width = rectDeckPos.Width;
                                panelMarkerDeck.Height = rectDeckPos.Height;

                                panelMarkerBoard.Top = rectBoardPos.Top;
                                panelMarkerBoard.Left = rectBoardPos.Left;
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

            timer1.Enabled = bHasValidMarkers;
            panelMarkerDeck.Visible = bHasValidMarkers;
            panelMarkerBoard.Visible = bHasValidMarkers;
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

        private void button1_Click(object sender, EventArgs e)
        {
            screenReader.DoWork(ScreenshotAnalyzer.EMode.All);
            UpdateScreenState(screenReader);
            OnUpdateState.Invoke();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            panelMarkerDeck.Visible = false;
            panelMarkerBoard.Visible = false;
            timer1.Enabled = false;
        }
    }
}
