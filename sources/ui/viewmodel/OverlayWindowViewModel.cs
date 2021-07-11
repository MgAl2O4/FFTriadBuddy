using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FFTriadBuddy.UI
{
    public class OverlayWindowViewModel : LocalizedViewModel, IOverlayWindowViewModel
    {
        public MainWindowViewModel MainWindow { get; private set; }

        public TriadGameScreenMemory ScreenMemory = new TriadGameScreenMemory();
        private ScreenAnalyzer ScreenAnalyzer;

        public class ScreenCoordVM
        {
            public enum Mode
            {
                Default,
                SwapWarning,
                CapturePanel,
                AdjustedCapturePanel,
            }

            public System.Drawing.Point ScreenCoords { get; set; }
            public System.Drawing.Size ScreenSize { get; set; }
            public float Duration { get; set; }
            public Mode DrawMode { get; set; }
        }

        public class ScreenCardVM : ScreenCoordVM
        {
            public enum SolverResult
            {
                Win,
                Draw,
                Lose,
            }

            public SolverResult Result { get; set; }
        }

        public class ScreenCactpotVM : ScreenCoordVM
        {
            public enum LineType
            {
                Horizontal,
                Vertical,
                DiagSE,
                DiagSW,
            }

            public LineType Line { get; set; }
        }

        public class BoardInfoVM : BaseViewModel
        {
            private CardViewModel card;
            public CardViewModel Card { get => card; set => PropertySetAndNotify(value, ref card); }

            private int cactpotNum = 0;
            public int CactpotNum { get => cactpotNum; set => PropertySetAndNotify(value, ref cactpotNum); }
        }

        private ScreenCoordVM markerCapturePanel;
        public ScreenCoordVM MarkerCapturePanel { get => markerCapturePanel; set => PropertySetAndNotify(value, ref markerCapturePanel); }
        private bool canMoveCapturePanelOnScan;

        private ScreenCoordVM markerSwapWarning;
        public ScreenCoordVM MarkerSwapWarning { get => markerSwapWarning; set => PropertySetAndNotify(value, ref markerSwapWarning); }

        private ScreenCoordVM markerSwapCard;
        public ScreenCoordVM MarkerSwapCard { get => markerSwapCard; set => PropertySetAndNotify(value, ref markerSwapCard); }

        private ScreenCoordVM markerCactpotCircle;
        public ScreenCoordVM MarkerCactpotCircle { get => markerCactpotCircle; set => PropertySetAndNotify(value, ref markerCactpotCircle); }

        private ScreenCardVM markerDeck;
        public ScreenCardVM MarkerDeck { get => markerDeck; set => PropertySetAndNotify(value, ref markerDeck); }

        private ScreenCardVM markerBoard;
        public ScreenCardVM MarkerBoard { get => markerBoard; set => PropertySetAndNotify(value, ref markerBoard); }

        private ScreenCactpotVM markerCactpotLine;
        public ScreenCactpotVM MarkerCactpotLine { get => markerCactpotLine; set => PropertySetAndNotify(value, ref markerCactpotLine); }

        public DeckViewModel BlueDeck { get; } = new DeckViewModel() { IsShowingDetails = false };
        public DeckViewModel RedDeck { get; } = new DeckViewModel() { IsShowingDetails = false };
        public CardCollectionViewModel RedKnownCards { get; } = new CardCollectionViewModel();
        public CardCollectionViewModel RedUnknownCards { get; } = new CardCollectionViewModel();

        public BulkObservableCollection<BoardInfoVM> Board { get; } = new BulkObservableCollection<BoardInfoVM>();

        private int scanId;
        public int ScanId { get => scanId; set { PropertySetAndNotify(value, ref scanId); OnPropertyChanged("DescScanId"); } }
        public string DescScanId => string.Format(loc.strings.OverlayForm_Details_ScanId, scanId);

        private int numRedPlaced;
        public int NumRedPlaced { get => numRedPlaced; set { PropertySetAndNotify(value, ref numRedPlaced); OnPropertyChanged("DescRedPlaced"); } }
        public string DescRedPlaced => string.Format(loc.strings.OverlayForm_Details_RedPlacedAll, numRedPlaced);

        private int numRedVarPlaced;
        public int NumRedVarPlaced { get => numRedVarPlaced; set { PropertySetAndNotify(value, ref numRedVarPlaced); OnPropertyChanged("DescRedVarPlaced"); } }
        public string DescRedVarPlaced => string.Format(loc.strings.OverlayForm_Details_RedPlacedVariable, numRedVarPlaced);

        private string descNpc;
        public string DescNpc { get => descNpc; set => PropertySetAndNotify(value, ref descNpc); }

        private string descRules;
        public string DescRules { get => descRules; set => PropertySetAndNotify(value, ref descRules); }

        private string descAnalyzerState;
        public string DescAnalyzerState { get => descAnalyzerState; set => PropertySetAndNotify(value, ref descAnalyzerState); }

        private BitmapSource analyzerStateIcon;
        public BitmapSource AnalyzerStateIcon { get => analyzerStateIcon; set => PropertySetAndNotify(value, ref analyzerStateIcon); }

        private bool useAutoScan;
        public bool UseAutoScan
        {
            get => useAutoScan;
            set
            {
                PropertySetAndNotify(value, ref useAutoScan);
                if (!value)
                {
                    DisableAutoScanTimers();
                }
            }
        }

        private bool isAutoScanActive;
        public bool IsAutoScanActive { get => isAutoScanActive; set => PropertySetAndNotify(value, ref isAutoScanActive); }

        private bool useDetails;
        public bool UseDetails
        {
            get => useDetails;
            set
            {
                bool oldDD = ShowDetailsDeck;
                bool oldDB = ShowDetailsBoard;

                if (value != useDetails)
                {
                    PropertySetAndNotify(value, ref useDetails);
                }

                if (oldDD != ShowDetailsDeck)
                {
                    OnPropertyChanged("ShowDetailsDeck");
                }

                if (oldDB != ShowDetailsBoard)
                {
                    OnPropertyChanged("ShowDetailsBoard");
                }
            }
        }
        public bool ShowDetailsDeck => (ScreenAnalyzer == null) || (useDetails && (ScreenAnalyzer?.activeScanner is ScannerTriad));
        public bool ShowDetailsBoard => (ScreenAnalyzer == null) || useDetails;

        private Dictionary<Icon, BitmapSource> cachedIcons = new Dictionary<Icon, BitmapSource>();
        private DispatcherTimer timerAutoScan;
        private DispatcherTimer timerAutoScanUpkeep;
        private bool canStopAutoCapture = true;
        private bool canRunAutoCapture = false;

        public ICommand CommandCapture { get; private set; }
        public ICommand CommandToggleDetails { get; private set; }

        private float markerDurationCard = 4.0f;
        private float markerDurationCactpot = 2.0f;
        private float markerDurationSwapCard = 4.0f;
        private float markerDurationSwapWarning = 8.0f;

        public string OverlayForm_Capture_AutoScan => loc.strings.OverlayForm_Capture_AutoScan;
        public string OverlayForm_Capture_Button => loc.strings.OverlayForm_Capture_Button;
        public string OverlayForm_Capture_Details => loc.strings.OverlayForm_Capture_Details;
        public string OverlayForm_Capture_Status => loc.strings.OverlayForm_Capture_Status;
        public string OverlayForm_CardInfo_Swapped => loc.strings.OverlayForm_CardInfo_Swapped;
        public string OverlayForm_DeckInfo_Mismatch => loc.strings.OverlayForm_DeckInfo_Mismatch;
        public string OverlayForm_Details_RedDeck => loc.strings.OverlayForm_Details_RedDeck;
        public string OverlayForm_Details_RedInfo => loc.strings.OverlayForm_Details_RedInfo;

        public OverlayWindowViewModel()
        {
            // design time only
        }

        public OverlayWindowViewModel(MainWindowViewModel mainVM)
        {
            MainWindow = mainVM;
            ScreenAnalyzer = mainVM.PageScreenshot.ScreenAnalyzer;

            for (int idx = 0; idx < 9; idx++)
            {
                Board.Add(new BoardInfoVM() { Card = new CardViewModel() { IsShowingDetails = false } });
            }

            CommandCapture = new RelayCommand<object>(CommandCapureFunc);
            CommandToggleDetails = new RelayCommand<bool>((wantsDetails) => UseDetails = wantsDetails);

            mainVM.GameModel.OnDeckChanged += (deck) => ScreenMemory.UpdatePlayerDeck(deck);
            mainVM.GameModel.OnNpcChanged += GameModel_OnNpcChanged;
            ScreenMemory.UpdatePlayerDeck(mainVM.GameModel.PlayerDeck);
            GameModel_OnNpcChanged(mainVM.GameModel.Npc);

            timerAutoScanUpkeep = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(15) };
            timerAutoScanUpkeep.Tick += TimerAutoScanUpkeep_Tick;

            timerAutoScan = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(0.25) };
            timerAutoScan.Tick += TimerAutoScan_Tick;

            UpdateAutoScanState();
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();

            UpdateAnalyzerDesc();
            UpdateCachedText();
            OnPropertyChanged("DescRedPlaced");
            OnPropertyChanged("DescRedVarPlaced");
        }

        private void GameModel_OnNpcChanged(TriadNpc npcOb)
        {
            UpdateAnalyzerDesc();
            UpdateCachedText();
        }

        private void CommandCapureFunc(object dummyParam)
        {
            ScreenAnalyzer.DoWork(ScreenAnalyzer.EMode.Default);

            UpdateScreenState();
            MainWindow.PageScreenshot.UpdateState();
        }

        public void OnOverlayActive()
        {
            UpdateAnalyzerDesc();

            // multi monitor setup: make sure that overlay and game and on the same monitor
            var screenBounds = ViewModelServices.OverlayWindow.GetScreenBounds(ScreenAnalyzer != null ? ScreenAnalyzer.screenReader.GetCachedGameWindow() : Rectangle.Empty);
            var newMarker = new ScreenCoordVM()
            {
                DrawMode = ScreenCoordVM.Mode.CapturePanel,
                ScreenCoords = new System.Drawing.Point(screenBounds.X + (int)screenBounds.Width / 2, screenBounds.Y + screenBounds.Bottom)
            };
            MarkerCapturePanel = newMarker;

            // allow one time auto placement on successful scan
            canMoveCapturePanelOnScan = true;
        }

        public void UpdateScreenState(bool debugMode = false)
        {
            // refresh details (depended on scanner)
            UseDetails = useDetails;

            if (ScreenAnalyzer.GetCurrentState() != ScreenAnalyzer.EState.NoErrors)
            {
                Logger.WriteLine("Capture failed: " + ScreenAnalyzer.GetCurrentState());
                UpdateAnalyzerDesc();
                return;
            }

            ScanId++;
            Logger.WriteLine("Capture scanId:{0}", ScanId);

            // multi monitor setup: make sure that overlay and game and on the same monitor
            Rectangle gameWindowRect = ScreenAnalyzer.screenReader.GetCachedGameWindow();
            ViewModelServices.OverlayWindow.OnProcessingGameWindow(gameWindowRect, out bool invalidatedPositions);
            if (invalidatedPositions)
            {
                canMoveCapturePanelOnScan = true;
            }

            Rectangle boardBox = Rectangle.Empty;

            var updateFlags = TriadGameScreenMemory.EUpdateFlags.None;
            if (ScreenAnalyzer.activeScanner is ScannerTriad)
            {
                boardBox = ScreenAnalyzer.scannerTriad.GetBoardBox();

                // solver logic
                updateFlags = ScreenMemory.OnNewScan(ScreenAnalyzer.scannerTriad.cachedGameState, MainWindow.GameModel.Npc);
                if (updateFlags != TriadGameScreenMemory.EUpdateFlags.None)
                {
                    ScreenMemory.gameSession.SolverFindBestMove(ScreenMemory.gameState, out int solverBoardPos, out TriadCard solverTriadCard, out var bestChance);

                    int blueCardIdx = ScreenMemory.deckBlue.GetCardIndex(solverTriadCard);
                    int boardCardIdx = (blueCardIdx < 0) ? -1 : solverBoardPos;

                    Logger.WriteLine("  suggested move: [{0}] {1} {2} (expected: {3})",
                        boardCardIdx, ETriadCardOwner.Blue,
                        solverTriadCard != null ? solverTriadCard.Name.GetCodeName() : "??",
                        bestChance.expectedResult);

                    if (blueCardIdx >= 0 && boardCardIdx >= 0)
                    {
                        try
                        {
                            Rectangle rectDeckPos = ScreenAnalyzer.scannerTriad.GetBlueCardBox(blueCardIdx);
                            var newMarkerDeck = new ScreenCardVM() { DrawMode = ScreenCoordVM.Mode.Default, Duration = markerDurationCard };
                            AssignGameBoundsToMarker(newMarkerDeck, rectDeckPos);

                            Rectangle rectBoardPos = ScreenAnalyzer.scannerTriad.GetBoardCardBox(boardCardIdx);
                            var newMarkerBoard = new ScreenCardVM() { DrawMode = ScreenCoordVM.Mode.Default, Duration = markerDurationCard };
                            AssignGameBoundsToMarker(newMarkerBoard, rectBoardPos);

                            switch (bestChance.expectedResult)
                            {
                                case ETriadGameState.BlueWins: newMarkerBoard.Result = ScreenCardVM.SolverResult.Win; break;
                                case ETriadGameState.BlueDraw: newMarkerBoard.Result = ScreenCardVM.SolverResult.Draw; break;
                                default: newMarkerBoard.Result = ScreenCardVM.SolverResult.Lose; break;
                            }

                            MarkerBoard = newMarkerBoard;
                            MarkerDeck = newMarkerDeck;
                        }
                        catch (Exception) { }
                    }
                }
                else
                {
                    // refresh markers
                    var tempBoard = markerBoard;
                    var tempDeck = markerDeck;
                    MarkerBoard = null; MarkerBoard = tempBoard;
                    MarkerDeck = null; MarkerDeck = tempDeck;
                }
            }
            else if (ScreenAnalyzer.activeScanner is ScannerCactpot)
            {
                boardBox = ScreenAnalyzer.scannerCactpot.GetBoardBox();

                // solver logic
                if (ScreenAnalyzer.scannerCactpot.cachedGameState.numRevealed > 3)
                {
                    CactpotGame.FindBestLine(ScreenAnalyzer.scannerCactpot.cachedGameState.board, out int fromIdx, out int toIdx);
                    Logger.WriteLine("  suggested line: [{0}] -> [{1}]", fromIdx, toIdx);

                    if (fromIdx >= 0 && toIdx >= 0)
                    {
                        var newMarker = new ScreenCactpotVM() { Duration = markerDurationCactpot };
                        Rectangle gameFromBox = ScreenAnalyzer.scannerCactpot.GetCircleBox(fromIdx);
                        Rectangle gameToBox = ScreenAnalyzer.scannerCactpot.GetCircleBox(toIdx);

                        Rectangle gameCombinedBox = Rectangle.Union(gameFromBox, gameToBox);
                        AssignGameBoundsToMarker(newMarker, gameCombinedBox);

                        newMarker.Line =
                            (gameFromBox.X == gameToBox.X) ? ScreenCactpotVM.LineType.Vertical :
                            (gameFromBox.Y == gameToBox.Y) ? ScreenCactpotVM.LineType.Horizontal :
                            (gameFromBox.X < gameToBox.X) ? ScreenCactpotVM.LineType.DiagSE :
                            ScreenCactpotVM.LineType.DiagSW;
                        MarkerCactpotLine = newMarker;
                    }
                }
                else
                {
                    int markerPos = CactpotGame.FindNextCircle(ScreenAnalyzer.scannerCactpot.cachedGameState.board);
                    Logger.WriteLine("  suggested move: [{0}]", markerPos);

                    if (markerPos >= 0)
                    {
                        Rectangle gameBoardPos = ScreenAnalyzer.scannerCactpot.GetCircleBox(markerPos);

                        var newMarker = new ScreenCoordVM() { Duration = markerDurationCactpot };
                        AssignGameBoundsToMarker(newMarker, gameBoardPos);
                        MarkerCactpotCircle = newMarker;
                    }
                }

                Board.SuspendNotifies();
                for (int Idx = 0; Idx < ScreenAnalyzer.scannerCactpot.cachedGameState.board.Length; Idx++)
                {
                    Board[Idx].Card.CardModel = null;
                    Board[Idx].CactpotNum = ScreenAnalyzer.scannerCactpot.cachedGameState.board[Idx];
                }
                Board.ResumeNotifies();
            }

            // update overlay location if needed
            if (!boardBox.IsEmpty && canMoveCapturePanelOnScan)
            {
                canMoveCapturePanelOnScan = false;

                var newMarker = new ScreenCoordVM() { DrawMode = ScreenCoordVM.Mode.AdjustedCapturePanel };
                AssignGameBoundsToMarker(newMarker, boardBox, 0);
                MarkerCapturePanel = newMarker;
            }

            // update what's needed
            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.Modifiers) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                UpdateCachedText();
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.BlueDeck) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                SyncDeckVM(BlueDeck, ScreenAnalyzer.scannerTriad.cachedGameState.blueDeck);
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.RedDeck) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                UpdateRedDeckDetails();
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.Board) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                Board.SuspendNotifies();
                for (int Idx = 0; Idx < ScreenMemory.gameState.board.Length; Idx++)
                {
                    Board[Idx].CactpotNum = 0;
                    Board[Idx].Card.Assign(ScreenMemory.gameState.board[Idx]);
                    Board[Idx].Card.IsShowingDetails = false;
                }
                Board.ResumeNotifies();
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.SwapWarning) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                Rectangle ruleRect = ScreenAnalyzer.scannerTriad.GetRuleBox();
                if (gameWindowRect.Width > 0 && ruleRect.Width > 0)
                {
                    var newMarker = new ScreenCoordVM() { DrawMode = ScreenCoordVM.Mode.SwapWarning, Duration = markerDurationSwapWarning };
                    AssignGamePosToMarker(newMarker, ruleRect.Left, ruleRect.Top);
                    MarkerSwapWarning = newMarker;
                }
            }

            if ((updateFlags & TriadGameScreenMemory.EUpdateFlags.SwapHints) != TriadGameScreenMemory.EUpdateFlags.None)
            {
                Rectangle gameDeckPos = ScreenAnalyzer.scannerTriad.GetBlueCardBox(ScreenMemory.swappedBlueCardIdx);

                var newMarker = new ScreenCoordVM() { Duration = markerDurationSwapCard };
                AssignGameBoundsToMarker(newMarker, gameDeckPos, -10);
                MarkerSwapCard = newMarker;
            }

            if (IsUsingAutoScan())
            {
                canStopAutoCapture = false;
                canRunAutoCapture = false;
                timerAutoScan.IsEnabled = true;
                TimerAutoScan_Tick(null, null);
            }
        }

        private void UpdateCachedText()
        {
            string desc = "";
            foreach (TriadGameModifier mod in ScreenMemory.gameSession.modifiers)
            {
                if (desc.Length > 0) { desc += ", "; }
                desc += mod.GetLocalizedName();
            }

            DescRules = string.Format(loc.strings.OverlayForm_Details_Rules, (desc.Length > 0) ? desc : loc.strings.OverlayForm_Dynamic_RulesWaiting);

            var npcOb = MainWindow.GameModel.Npc;
            DescNpc = string.Format(loc.strings.OverlayForm_Details_Npc, (npcOb != null) ? npcOb.Name.GetLocalized() : loc.strings.OverlayForm_Dynamic_NpcUnknown);
        }

        private void UpdateRedDeckDetails()
        {
            var modelProxyDB = ModelProxyDB.Get();

            if (ScreenMemory.deckRed == null || ScreenMemory.deckRed.deck == null)
            {
                SyncDeckVM(RedDeck, null);
                SyncDeckVM(RedKnownCards, null);
                SyncDeckVM(RedUnknownCards, null);

                NumRedPlaced = 0;
                NumRedVarPlaced = 0;
            }
            else
            {
                SyncDeckVM(RedDeck, ScreenMemory.deckRed.cards);

                SyncDeckVM(RedKnownCards, ScreenMemory.deckRed.deck.knownCards);
                int deckIdx = 0;
                for (int idx = 0; idx < RedKnownCards.Cards.Count; idx++, deckIdx++)
                {
                    RedKnownCards.Cards[idx].IsPreview = ScreenMemory.deckRed.IsPlaced(deckIdx);

                    if (ScreenMemory.deckRed.swappedCardIdx == deckIdx)
                    {
                        RedKnownCards.Cards[idx].CardModel = modelProxyDB.GetCardProxy(ScreenMemory.deckRed.swappedCard);
                    }
                }

                SyncDeckVM(RedUnknownCards, ScreenMemory.deckRed.deck.unknownCardPool);
                for (int idx = 0; idx < RedUnknownCards.Cards.Count; idx++, deckIdx++)
                {
                    RedUnknownCards.Cards[idx].IsPreview = ScreenMemory.deckRed.IsPlaced(deckIdx);

                    if (ScreenMemory.deckRed.swappedCardIdx == deckIdx)
                    {
                        RedUnknownCards.Cards[idx].CardModel = modelProxyDB.GetCardProxy(ScreenMemory.deckRed.swappedCard);
                    }
                }

                NumRedPlaced = ScreenMemory.deckRed.numPlaced;
                NumRedVarPlaced = ScreenMemory.deckRed.numUnknownPlaced;
            }
        }

        private void TimerAutoScan_Tick(object sender, EventArgs e)
        {
            bool bDebugMode = false;
            UpdateAutoScanState();

            bool attemptScan = (ScreenAnalyzer != null) && (ScreenAnalyzer.activeScanner == null || ScreenAnalyzer.activeScanner is ScannerTriad);
            if (attemptScan)
            {
                if (timerAutoScanUpkeep.IsEnabled)
                {
                    // always retry in upkeep mode
                }
                else if (ScreenAnalyzer.scannerTriad.cachedGameState == null ||
                    canStopAutoCapture && ScreenAnalyzer.scannerTriad.cachedGameState.turnState == ScannerTriad.ETurnState.MissingTimer)
                {
                    attemptScan = false;
                    canRunAutoCapture = false;

                    var isOverlayActive = MainWindow.PageScreenshot.IsOverlayActive;
                    var wantsAutoScan = IsUsingAutoScan();
                    if (isOverlayActive && wantsAutoScan)
                    {
                        Logger.WriteLine("Auto scan: entering upkeep mode");
                        timerAutoScanUpkeep.Start();
                    }
                    else
                    {
                        timerAutoScan.Stop();
                    }
                }
            }

            if (attemptScan)
            {
                Rectangle timerGameBox = ScreenAnalyzer.scannerTriad.GetTimerScanBox();
                ScreenAnalyzer.scanClipBounds = timerGameBox;

                ScreenAnalyzer.DoWork(ScreenAnalyzer.EMode.ScanTriad | ScreenAnalyzer.EMode.NeverResetCache, (int)ScannerTriad.EScanMode.TimerOnly);

                ScreenAnalyzer.scanClipBounds = Rectangle.Empty;
                canStopAutoCapture = true;
            }

            UpdateAnalyzerDesc();

            if (ScreenAnalyzer != null && ScreenAnalyzer.scannerTriad.cachedGameState != null && IsUsingAutoScan())
            {
                ScannerTriad.ETurnState turnState = ScreenAnalyzer.scannerTriad.cachedGameState.turnState;
                if (turnState != ScannerTriad.ETurnState.MissingTimer && timerAutoScanUpkeep.IsEnabled)
                {
                    Logger.WriteLine("Auto scan: aborting upkeep mode (scanned)");
                    timerAutoScanUpkeep.Stop();
                }

                if (turnState == ScannerTriad.ETurnState.Waiting)
                {
                    canRunAutoCapture = true;
                }
                else if (turnState == ScannerTriad.ETurnState.Active)
                {
                    if (canRunAutoCapture)
                    {
                        bool bIsMouseOverGrid = IsCursorInScanArea();
                        if (bDebugMode || true) { Logger.WriteLine("Checking auto scan: mouse:{0}, state:{1}", bIsMouseOverGrid ? "OverGrid" : "ok", ScreenAnalyzer.GetCurrentState()); }

                        if (!bIsMouseOverGrid && ScreenAnalyzer.GetCurrentState() == ScreenAnalyzer.EState.NoErrors)
                        {
                            canRunAutoCapture = false;
                            CommandCapture.Execute(null);
                        }
                    }
                }
            }
        }

        private void TimerAutoScanUpkeep_Tick(object sender, EventArgs e)
        {
            Logger.WriteLine("Auto scan: upkeep mode timed out");
            DisableAutoScanTimers();
        }

        private void DisableAutoScanTimers()
        {
            Logger.WriteLine("Auto scan: disabled");
            canRunAutoCapture = false;
            timerAutoScanUpkeep.Stop();
            timerAutoScan.Stop();
            UpdateAutoScanState();
        }

        private void SyncDeckVM(CardCollectionViewModel deckVM, IList<TriadCard> cards)
        {
            if (cards == null)
            {
                deckVM.Cards.Clear();
                return;
            }

            while (deckVM.Cards.Count > cards.Count)
            {
                deckVM.Cards.RemoveAt(deckVM.Cards.Count - 1);
            }

            while (deckVM.Cards.Count < cards.Count)
            {
                deckVM.Cards.Add(new CardViewModel());
            }

            var modelProxyDB = ModelProxyDB.Get();
            int hiddenCardId = TriadCardDB.Get().hiddenCard.Id;

            for (int idx = 0; idx < cards.Count; idx++)
            {
                var cardVM = deckVM.Cards[idx];
                cardVM.CardModel = modelProxyDB.GetCardProxy(cards[idx]);
                cardVM.IsHidden = cards[idx]?.Id == hiddenCardId;
            }
        }

        private void AssignGameBoundsToMarker(ScreenCoordVM markerVM, Rectangle gameBounds, int inflateSize = 10)
        {
            Rectangle screenBounds = ScreenAnalyzer.ConvertGameToScreen(gameBounds);
            if (screenBounds.Width == 0)
            {
                // screen reader not initialized / not tied to game window
                screenBounds = new Rectangle(gameBounds.Location, gameBounds.Size);
            }

            screenBounds.Inflate(inflateSize, inflateSize);

            markerVM.ScreenCoords = screenBounds.Location;
            markerVM.ScreenSize = screenBounds.Size;
        }

        private void AssignGamePosToMarker(ScreenCoordVM markerVM, int gamePosX, int gamePosY)
        {
            Rectangle screenBounds = ScreenAnalyzer.ConvertGameToScreen(new Rectangle(gamePosX, gamePosY, 100, 100));
            if (screenBounds.Width == 0)
            {
                // screen reader not initialized / not tied to game window
                screenBounds = new Rectangle(gamePosX, gamePosY, 0, 0);
            }

            markerVM.ScreenCoords = screenBounds.Location;
            markerVM.ScreenSize = System.Drawing.Size.Empty;
        }

        private bool IsCursorInScanArea()
        {
            Rectangle screenScanArea = ScreenAnalyzer.ConvertGameToScreen(ScreenAnalyzer.currentScanArea);
            return ViewModelServices.OverlayWindow.IsCursorInside(screenScanArea);
        }

        private bool IsUsingAutoScan()
        {
            bool bIsAutoScanAllowed = (ScreenAnalyzer == null) || (ScreenAnalyzer.activeScanner is ScannerTriad);
            return useAutoScan && bIsAutoScanAllowed;
        }

        private void UpdateAutoScanState()
        {
            bool newIsActive = IsUsingAutoScan() && (ScreenAnalyzer != null);
            if (newIsActive)
            {
                if (timerAutoScanUpkeep.IsEnabled)
                {
                    // always active during upkeep
                }
                else if (ScreenAnalyzer.scannerTriad.cachedGameState != null)
                {
                    newIsActive = ScreenAnalyzer.scannerTriad.cachedGameState.turnState != ScannerTriad.ETurnState.MissingTimer;
                }
            }

            IsAutoScanActive = newIsActive;
        }

        private BitmapSource FindOrAddIcon(Icon icon)
        {
            if (!cachedIcons.ContainsKey(icon))
            {
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                cachedIcons.Add(icon, bitmap);
            }

            return cachedIcons[icon];
        }

        private void UpdateAnalyzerDesc()
        {
            if (ScreenAnalyzer == null)
            {
                return;
            }

            var showState = ScreenAnalyzer.GetCurrentState();
            switch (showState)
            {
                case ScreenAnalyzer.EState.NoInputImage:
                    AnalyzerStateIcon = FindOrAddIcon(SystemIcons.Error);
                    switch (ScreenAnalyzer.screenReader.currentState)
                    {
                        case ScreenReader.EState.MissingGameProcess: DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_MissingGameProcess; break;
                        case ScreenReader.EState.MissingGameWindow: DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_MissingGameWindow; break;
                        default: DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_NoInputImage; break;
                    }
                    break;

                case ScreenAnalyzer.EState.NoScannerMatch:
                    AnalyzerStateIcon = FindOrAddIcon(SystemIcons.Error);
                    DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_NoScannerMatch;
                    break;

                case ScreenAnalyzer.EState.UnknownHash:
                    AnalyzerStateIcon = FindOrAddIcon(SystemIcons.Warning);
                    DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_UnknownHash;
                    break;

                case ScreenAnalyzer.EState.ScannerErrors:
                    AnalyzerStateIcon = FindOrAddIcon(SystemIcons.Error);
                    DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_ScannerErrors;

                    if (ScreenAnalyzer.activeScanner is ScannerTriad)
                    {
                        switch (ScreenAnalyzer.scannerTriad.cachedScanError)
                        {
                            case ScannerTriad.EScanError.MissingGrid: DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_MissingGrid; break;
                            case ScannerTriad.EScanError.MissingCards: DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_MissingCards; break;
                            case ScannerTriad.EScanError.FailedCardMatching:
                                DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_FailedCardMatching;
                                AnalyzerStateIcon = FindOrAddIcon(SystemIcons.Warning);
                                break;

                            default: break;
                        }
                    }
                    break;

                default:
                    {
                        string npcDesc = (MainWindow.GameModel.Npc != null) ? (MainWindow.GameModel.Npc.Name.GetLocalized() + ": ") : "";
                        AnalyzerStateIcon = FindOrAddIcon(SystemIcons.Information);

                        if (ScreenAnalyzer.activeScanner == null || ScreenAnalyzer.activeScanner.cachedGameStateBase == null)
                        {
                            DescAnalyzerState = npcDesc + loc.strings.OverlayForm_Dynamic_Status_Ready;
                        }
                        else if (ScreenAnalyzer.activeScanner is ScannerTriad)
                        {
                            switch (ScreenAnalyzer.scannerTriad.cachedGameState.turnState)
                            {
                                case ScannerTriad.ETurnState.MissingTimer:
                                    DescAnalyzerState = npcDesc + loc.strings.OverlayForm_Dynamic_Status_Ready;
                                    break;

                                case ScannerTriad.ETurnState.Waiting:
                                    DescAnalyzerState = npcDesc + loc.strings.OverlayForm_Dynamic_Status_WaitingForTurn;
                                    AnalyzerStateIcon = FindOrAddIcon(SystemIcons.Shield);
                                    break;

                                default:
                                    if (IsUsingAutoScan() && IsCursorInScanArea())
                                    {
                                        DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_AutoScanMouseOverBoard;
                                        AnalyzerStateIcon = FindOrAddIcon(SystemIcons.Warning);
                                    }
                                    else
                                    {
                                        DescAnalyzerState = npcDesc + loc.strings.OverlayForm_Dynamic_Status_ActiveTurn;
                                    }
                                    break;
                            }
                        }
                        else if (ScreenAnalyzer.activeScanner is ScannerCactpot)
                        {
                            DescAnalyzerState = loc.strings.OverlayForm_Dynamic_Status_CactpotReady;
                        }
                        else
                        {
                            DescAnalyzerState = npcDesc + loc.strings.OverlayForm_Dynamic_Status_Ready;
                        }
                    }
                    break;
            }
        }
    }
}
