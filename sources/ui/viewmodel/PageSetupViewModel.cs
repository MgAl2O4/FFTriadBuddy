using MgAl2O4.GoogleAPI;
using System;
using System.Collections.Generic;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace FFTriadBuddy.UI
{
    public class PageSetupViewModel : LocalizedViewModel
    {
        public MainWindowViewModel MainWindow;
        public TriadDeckOptimizer DeckOptimizer = new TriadDeckOptimizer();
        public SolvableDeckViewModel DeckSolver { get; } = new SolvableDeckViewModel();
        public SetupFavDeckViewModel FavDeckVM { get; } = new SetupFavDeckViewModel();

        public List<NpcModelProxy> Npcs => ModelProxyDB.Get().Npcs;
        public List<RuleModelProxy> Rules => ModelProxyDB.Get().Rules;
        public List<TournamentModelProxy> Tournaments => ModelProxyDB.Get().Tournaments;

        private NpcModelProxy activeNpc;
        public NpcModelProxy ActiveNpc
        {
            get => activeNpc;
            set
            {
                if (value == null)
                {
                    // nope, force going back to previous one
                    OnPropertyChanged();
                }
                else if (value != activeNpc)
                {
                    PropertySetAndNotify(value, ref activeNpc);
                    MainWindow.GameModel.SetNpc(activeNpc?.npcOb);
                }
            }
        }

        private RuleModelProxy activeRegionRule1;
        public RuleModelProxy ActiveRegionRule1 { get => activeRegionRule1; set { PropertySetAndNotify(value, ref activeRegionRule1); UpdateModelRules(); } }

        private RuleModelProxy activeRegionRule2;
        public RuleModelProxy ActiveRegionRule2 { get => activeRegionRule2; set { PropertySetAndNotify(value, ref activeRegionRule2); UpdateModelRules(); } }

        private TournamentModelProxy activeTournament;
        public TournamentModelProxy ActiveTournament { get => activeTournament; set { PropertySetAndNotify(value, ref activeTournament); UpdateModelRules(); } }

        private readonly DeckViewModel activeDeck = new DeckViewModel();
        public DeckViewModel ActiveDeck => DeckSolver.Deck as DeckViewModel;

        private string descDeckState;
        public string DescDeckState { get => descDeckState; set { if (value != descDeckState) { PropertySetAndNotify(value, ref descDeckState); } } }

        private bool isRegionMode = true;
        public bool IsRegionMode { get => isRegionMode; set { PropertySetAndNotify(value, ref isRegionMode); UpdateModelRules(); } }

        private bool isCloudSaveEnabled = false;
        public bool IsCloudSaveEnabled { get => isCloudSaveEnabled; set => PropertySetAndNotify(value, ref isCloudSaveEnabled); }

        private bool isCloudSaveButtonActive = false;
        public bool IsCloudSaveButtonActive { get => isCloudSaveButtonActive; set => PropertySetAndNotify(value, ref isCloudSaveButtonActive); }

        private bool isDeckOptimizerRunning = false;
        public bool IsDeckOptimizerRunning { get => isDeckOptimizerRunning; set { PropertySetAndNotify(value, ref isDeckOptimizerRunning); OnPropertyChanged("IsDeckOptimizerStopped"); } }
        public bool IsDeckOptimizerStopped => !isDeckOptimizerRunning;

        private string cloudSaveStatus;
        public string CloudSaveStatus { get => cloudSaveStatus; set => PropertySetAndNotify(value, ref cloudSaveStatus); }

        private int optimizerProgress;
        public int OptimizerProgress { get => optimizerProgress; set { PropertySetAndNotify(value, ref optimizerProgress); OnPropertyChanged("OptimizerProgressDesc"); } }
        public string OptimizerProgressDesc => DeckOptimizer.IsAborted() ? string.Format(loc.strings.MainForm_Dynamic_Setup_OptimizerProgressAborted, optimizerProgress * 0.01f) : (optimizerProgress + "%");

        public int OptimizerNumOwned => ModelProxyDB.Get().OwnedCards.Count;

        private string optimizerNumTestedDesc;
        public string OptimizerNumTestedDesc { get => optimizerNumTestedDesc; set => PropertySetAndNotify(value, ref optimizerNumTestedDesc); }

        private string optimizerNumPossibleDesc;
        public string OptimizerNumPossibleDesc { get => optimizerNumPossibleDesc; set => PropertySetAndNotify(value, ref optimizerNumPossibleDesc); }

        private string optimizerTimeLeftDesc;
        public string OptimizerTimeLeftDesc { get => optimizerTimeLeftDesc; set => PropertySetAndNotify(value, ref optimizerTimeLeftDesc); }

        private TriadDeck optimizerFoundDeck;
        private bool optimizerFoundDeckDelay = false;

        public ICommand CommandPickNpc { get; private set; }
        public ICommand CommandToggleTournament { get; private set; }
        public ICommand CommandToggleCloudSaves { get; private set; }
        public ICommand CommandCloudAuth { get; private set; }
        public ICommand CommandDeckOptimizerStart { get; private set; }
        public ICommand CommandDeckOptimizerAbort { get; private set; }

        public string MainForm_Setup_Cloud_AuthButton => loc.strings.MainForm_Setup_Cloud_AuthButton;
        public string MainForm_Setup_Cloud_Desc => loc.strings.MainForm_Setup_Cloud_Desc;
        public string MainForm_Setup_Deck_OptimizeAbortButton => loc.strings.MainForm_Setup_Deck_OptimizeAbortButton;
        public string MainForm_Setup_Deck_OptimizeStartButton => loc.strings.MainForm_Setup_Deck_OptimizeStartButton;
        public string MainForm_Setup_Deck_Title => loc.strings.MainForm_Setup_Deck_Title;
        public string MainForm_Setup_Fav_AddSlotButton => loc.strings.MainForm_Setup_Fav_AddSlotButton;
        public string MainForm_Setup_NPC => loc.strings.MainForm_Setup_NPC;
        public string MainForm_Setup_NPC_DeckPower => loc.strings.MainForm_Setup_NPC_DeckPower;
        public string MainForm_Setup_NPC_Location => loc.strings.MainForm_Setup_NPC_Location;
        public string MainForm_Setup_NPC_Rules => loc.strings.MainForm_Setup_NPC_Rules;
        public string MainForm_Setup_NPC_WinChance => loc.strings.MainForm_Setup_NPC_WinChance;
        public string MainForm_Setup_OptimizerStats_NumOwned => loc.strings.MainForm_Setup_OptimizerStats_NumOwned;
        public string MainForm_Setup_OptimizerStats_NumPossible => loc.strings.MainForm_Setup_OptimizerStats_NumPossible;
        public string MainForm_Setup_OptimizerStats_NumTested => loc.strings.MainForm_Setup_OptimizerStats_NumTested;
        public string MainForm_Setup_OptimizerStats_Progress => loc.strings.MainForm_Setup_OptimizerStats_Progress;
        public string MainForm_Setup_OptimizerStats_TimeLeft => loc.strings.MainForm_Setup_OptimizerStats_TimeLeft;
        public string MainForm_Setup_OptimizeStats_Title => loc.strings.MainForm_Setup_OptimizeStats_Title;
        public string MainForm_Setup_RulesToggle => loc.strings.MainForm_Setup_RulesToggle;
        public string MainForm_Setup_Rules_Region1 => loc.strings.MainForm_Setup_Rules_Region1;
        public string MainForm_Setup_Rules_Region2 => loc.strings.MainForm_Setup_Rules_Region2;
        public string MainForm_Setup_Rules_Tournament => loc.strings.MainForm_Setup_Rules_Tournament;
        public string MainForm_Setup_Rules_TournamentRules => loc.strings.MainForm_Setup_Rules_TournamentRules;
        public string FavDeckCtrl_Edit => loc.strings.FavDeckCtrl_Edit;
        public string FavDeckForm_Dynamic_UpdateButton => loc.strings.FavDeckForm_Dynamic_UpdateButton;
        public string FavDeckForm_RemoveButton => loc.strings.FavDeckForm_RemoveButton;

        public PageSetupViewModel()
        {
            // design time only
        }

        public PageSetupViewModel(MainWindowViewModel mainVM)
        {
            MainWindow = mainVM;

            CommandPickNpc = new RelayCommand<NpcModelProxy>((npcProxy) => ActiveNpc = npcProxy);
            CommandToggleTournament = new RelayCommand<bool>((wantsRegionMode) => IsRegionMode = wantsRegionMode);
            CommandCloudAuth = new RelayCommand<object>((_) => SettingsModel.CloudStorageInit(), (_) => isCloudSaveButtonActive);
            CommandToggleCloudSaves = new RelayCommand<bool>(CommandToggleCloudSavesFunc);
            CommandDeckOptimizerStart = new RelayCommand<object>(CommandDeckOptimizerStartFunc);
            CommandDeckOptimizerAbort = new RelayCommand<object>((_) => DeckOptimizer.AbortProcess());

            DeckOptimizer.OnFoundDeck += DeckOptimizer_OnFoundDeck;
            DeckSolver.Deck = activeDeck;
            DeckSolver.EnableTrackingProgress();
            DeckSolver.solver.OnSolved += (_, chance) => MainWindow.GameModel.SetCachedWinChance(chance);

            // force combo box initial values, assign underlying value to avoid setter's notifies
            var viewRules = CollectionViewSource.GetDefaultView(Rules);
            viewRules.MoveCurrentToFirst();
            activeRegionRule1 = activeRegionRule2 = viewRules.CurrentItem as RuleModelProxy;

            var viewTournaments = CollectionViewSource.GetDefaultView(Tournaments);
            viewTournaments.MoveCurrentToFirst();
            activeTournament = viewTournaments.CurrentItem as TournamentModelProxy;

            // setup game callbacks
            mainVM.GameModel.OnNpcChanged += GameModel_OnNpcChanged;
            mainVM.GameModel.OnDeckChanged += GameModel_OnDeckChanged;
            GameModel_OnNpcChanged(mainVM.GameModel.Npc);
            GameModel_OnDeckChanged(mainVM.GameModel.PlayerDeck);
            DeckSolver.RefreshSolver(mainVM.GameModel, mainVM.GameModel.PlayerDeck);

            ModelProxyDB.Get().OnCardOwnerChanged += PageSetupViewModel_OnCardOwnerChanged;
            activeDeck.OnCardsChanged += ActiveDeck_OnCardsChanged;

            // setup cloud saves
            SettingsModel.OnCloudStorageApiUpdate += SettingsModel_OnCloudStorageApiUpdate;
            SettingsModel.OnCloudStorageStateUpdate += SettingsModel_OnCloudStorageStateUpdate;
            CommandToggleCloudSavesFunc(PlayerSettingsDB.Get().useCloudStorage);

            // fav decks
            FavDeckVM.activeDeck = activeDeck;
            FavDeckVM.gameModel = mainVM.GameModel;
            FavDeckVM.Initialize();
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();

            // temp until all game clients are on new rule set
            DeckOptimizer.OnLanguageChanged();

            DeckSolver.RefreshLocalization();
            UpdateCloudStorageState();
            UpdateDeckState();
        }

        private void GameModel_OnNpcChanged(TriadNpc npcOb)
        {
            ActiveNpc = ModelProxyDB.Get().GetNpcProxy(npcOb);
        }

        private void GameModel_OnDeckChanged(TriadDeck deckOb)
        {
            if (activeDeck.Cards.Count != deckOb.knownCards.Count)
            {
                activeDeck.Cards.Clear();
                for (int idx = 0; idx < deckOb.knownCards.Count; idx++)
                {
                    activeDeck.Cards.Add(new CardViewModel() { CardOwner = ECardOwner.None });
                }
            }

            var modelProxyDB = ModelProxyDB.Get();
            for (int idx = 0; idx < deckOb.knownCards.Count; idx++)
            {
                activeDeck.Cards[idx].CardModel = modelProxyDB.GetCardProxy(deckOb.knownCards[idx]);
            }

            UpdateDeckState();
        }

        private void UpdateDeckState()
        {
            var deckState = MainWindow.GameModel.PlayerDeck.GetDeckState();
            switch (deckState)
            {
                case ETriadDeckState.TooMany5Star: DescDeckState = loc.strings.MainForm_Dynamic_DeckState_TooMany5Star; break;
                case ETriadDeckState.TooMany4Star: DescDeckState = loc.strings.MainForm_Dynamic_DeckState_TooMany4Star; break;
                case ETriadDeckState.MissingCards: DescDeckState = loc.strings.MainForm_Dynamic_DeckState_MissingCards; break;
                case ETriadDeckState.HasDuplicates: DescDeckState = loc.strings.MainForm_Dynamic_DeckState_HasDuplicates; break;
                default: DescDeckState = ""; break;
            }
        }

        private void PageSetupViewModel_OnCardOwnerChanged(CardModelProxy cardProxy)
        {
            if (MainWindow.GameModel.PlayerDeck.GetCardIndex(cardProxy.cardOb) >= 0)
            {
                UpdateDeckState();
            }

            OnPropertyChanged("OptimizerNumOwned");
        }

        private void ActiveDeck_OnCardsChanged(DeckViewModel deckVM)
        {
            var cards = new List<TriadCard>();
            foreach (var cardVM in deckVM.Cards)
            {
                cards.Add(cardVM.CardModel.cardOb);
            }

            MainWindow.GameModel.SetPlayerDeck(new TriadDeck(cards));
        }

        private void CommandToggleCloudSavesFunc(bool wantsCloudSaves)
        {
            IsCloudSaveEnabled = wantsCloudSaves;
            SettingsModel.SetUseCloudSaves(wantsCloudSaves);

            UpdateCloudStorageState();
        }

        private void UpdateCloudStorageState()
        {
            if (!IsCloudSaveEnabled)
            {
                CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_Disabled;
                IsCloudSaveButtonActive = false;
            }
            else if (SettingsModel.CloudStorage == null)
            {
                CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_NoDatabase;
                IsCloudSaveButtonActive = false;
            }
            else
            {
                SettingsModel.CloudStorageRequestState();
            }
        }

        private void SettingsModel_OnCloudStorageStateUpdate(SettingsModel.CloudSaveState state)
        {
            switch (state)
            {
                case SettingsModel.CloudSaveState.Loaded: CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudSave_Loaded; break;
                case SettingsModel.CloudSaveState.Saved: CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_Uploaded; break;
                case SettingsModel.CloudSaveState.UpToDate: CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_Synced; break;
                default: break;
            }
        }

        private void SettingsModel_OnCloudStorageApiUpdate(GoogleDriveService.EState state)
        {
            IsCloudSaveButtonActive = state == GoogleDriveService.EState.NotAuthorized;
            switch (state)
            {
                case GoogleDriveService.EState.NoErrors: CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_NoErrors; break;
                case GoogleDriveService.EState.ApiFailure: CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_ApiFailure; break;
                case GoogleDriveService.EState.NotAuthorized: CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_NotAuthorized; break;
                case GoogleDriveService.EState.AuthInProgress: CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_AuthInProgress; break;
                case GoogleDriveService.EState.NotInitialized: CloudSaveStatus = loc.strings.MainForm_Dynamic_Setup_CloudStatus_NotInitialized; break;
                default: CloudSaveStatus = ""; break;
            }
        }

        private async void CommandDeckOptimizerStartFunc(object dummyParam)
        {
            var lockedCards = new List<TriadCard>();
            foreach (var cardVM in activeDeck.Cards)
            {
                lockedCards.Add(cardVM.IsShowingLock ? cardVM.CardModel.cardOb : null);
            }

            DeckOptimizer.Initialize(MainWindow.GameModel.Npc, MainWindow.GameModel.Rules.ToArray(), lockedCards);

            OptimizerNumPossibleDesc = DeckOptimizer.GetNumPossibleDecksDesc();
            OptimizerNumTestedDesc = "0";
            OptimizerProgress = 0;
            OptimizerTimeLeftDesc = "--";
            IsDeckOptimizerRunning = true;

            var updateTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(0.25) };
            updateTimer.Tick += DeckOptimizerUpdateTimer_Tick;
            updateTimer.Start();

            await DeckOptimizer.Process(MainWindow.GameModel.Npc, MainWindow.GameModel.Rules.ToArray(), lockedCards);

            IsDeckOptimizerRunning = false;
            updateTimer.Stop();
            DeckOptimizerUpdateTimer_Tick(null, null);
            OptimizerTimeLeftDesc = "--";

            MainWindow.GameModel.SetPlayerDeck(DeckOptimizer.optimizedDeck);
        }

        private void DeckOptimizerUpdateTimer_Tick(object sender, EventArgs e)
        {
            OptimizerProgress = DeckOptimizer.GetProgress();
            OptimizerNumTestedDesc = DeckOptimizer.GetNumTestedDesc();

            if (sender != null)
            {
                var timerInterval = ((DispatcherTimer)sender).Interval;
                int secondsRemaining = DeckOptimizer.GetSecondsRemaining((int)timerInterval.TotalMilliseconds);
                var tspan = TimeSpan.FromSeconds(secondsRemaining);
                if (tspan.Hours > 0 || tspan.Minutes > 55)
                {
                    OptimizerTimeLeftDesc = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", tspan.Hours, tspan.Minutes, tspan.Seconds);
                }
                else if (tspan.Minutes > 0 || tspan.Seconds > 55)
                {
                    OptimizerTimeLeftDesc = string.Format("{0:D2}m:{1:D2}s", tspan.Minutes, tspan.Seconds);
                }
                else
                {
                    OptimizerTimeLeftDesc = string.Format("{0:D2}s", tspan.Seconds);
                }
            }
        }

        private void DeckOptimizer_OnFoundDeck(TriadDeck deck)
        {
            optimizerFoundDeck = deck;

            // delay to buffer multiple changes
            if (!optimizerFoundDeckDelay)
            {
                optimizerFoundDeckDelay = true;

                var bufferTimer = new DispatcherTimer(DispatcherPriority.Normal, App.Current.Dispatcher) { Interval = TimeSpan.FromSeconds(0.5) };
                bufferTimer.Tick += DeckOptimizerBufferTimer_Tick;
                bufferTimer.Start();
            }
        }

        private void DeckOptimizerBufferTimer_Tick(object sender, EventArgs e)
        {
            MainWindow.GameModel.SetPlayerDeck(optimizerFoundDeck);
            optimizerFoundDeckDelay = false;
            ((DispatcherTimer)sender).Stop();
        }

        private void UpdateModelRules()
        {
            if (isRegionMode)
            {
                MainWindow.GameModel.SetGameRules(activeRegionRule1?.modOb, activeRegionRule2?.modOb);
            }
            else if (activeTournament != null)
            {
                MainWindow.GameModel.SetGameRules(activeTournament.tournamentOb.Rules);
            }
            else
            {
                MainWindow.GameModel.SetGameRules(null);
            }
        }
    }
}
