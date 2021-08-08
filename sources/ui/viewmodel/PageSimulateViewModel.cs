using System.Collections.Generic;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class PageSimulateViewModel : LocalizedViewModel, ICardDragDropTarget
    {
        public class BoardCardVM
        {
            public CardViewModel Card { get; private set; }
            public int BoardX { get; private set; }
            public int BoardY { get; private set; }

            public BoardCardVM(int idx)
            {
                BoardX = idx % 3;
                BoardY = idx / 3;

                Card = new CardViewModel() { OwnerIndex = idx };
            }
        }

        public MainWindowViewModel MainWindow;
        public SimulateRulesViewModel SpecialRules { get; } = new SimulateRulesViewModel();

        public DeckViewModel PlayerDeck { get; } = new DeckViewModel();
        public DeckViewModel RedKnownCards { get; } = new DeckViewModel();
        public DeckViewModel RedUnknownCards { get; } = new DeckViewModel();
        public BulkObservableCollection<BoardCardVM> BoardCards { get; } = new BulkObservableCollection<BoardCardVM>();

        public WinChanceViewModel WinChance { get; } = new WinChanceViewModel();

        private string descRules;
        public string DescRules { get => descRules; set { if (value != descRules) { PropertySetAndNotify(value, ref descRules); } } }

        private bool waitingGameResult = false;
        private string descGameResult = null;
        public string DescChanceAndResult => descGameResult == null ? WinChance.DescProbability : descGameResult;

        private bool hasGameHint = false;
        public bool HasGameHint { get => hasGameHint; set { if (value != hasGameHint) { PropertySetAndNotify(value, ref hasGameHint); OnPropertyChanged("DescGameHint"); } } }
        public string DescGameHint => hasGameHint ? loc.strings.MainForm_Dynamic_Simulate_LastCardHint : loc.strings.MainForm_Simulate_Game_ListHint;

        private string descUndoButton;
        public string DescUndoButton { get => descUndoButton; set => PropertySetAndNotify(value, ref descUndoButton); }

        private int numUnknownToPlace = 0;
        public int NumUnknownToPlace { get => numUnknownToPlace; set { PropertySetAndNotify(value, ref numUnknownToPlace); OnPropertyChanged("DescUnknownCards"); OnPropertyChanged("HasUnknownCards"); } }
        public string DescUnknownCards => string.Format(loc.strings.MainForm_Simulate_Game_UnknownCards, numUnknownToPlace);
        public bool HasUnknownCards => numUnknownToPlace > 0;

        private bool canStartWithBlue = false;
        public bool CanStartWithBlue { get => canStartWithBlue; set => PropertySetAndNotify(value, ref canStartWithBlue); }

        private bool canSelectBlue = false;
        public bool CanSelectBlue { get => canSelectBlue; set => PropertySetAndNotify(value, ref canSelectBlue); }

        public int SpecialRuleSwitcherIdx => (int)SpecialRules.ActiveRule;
        public int SpecialRuleSwitcherBoardIdx => (SpecialRules.ActiveRule == SimulateRulesViewModel.Rule.None) ? 0 : 1;

        private TriadGameData cachedLastState;
        private TriadGameModel.Move cachedLastMove;

        public ICommand CommandReset { get; private set; }
        public ICommand CommandBlueFirst { get; private set; }
        public ICommand CommandRedUndo { get; private set; }

        public string MainForm_Simulate_Debug_ForceCached => loc.strings.MainForm_Simulate_Debug_ForceCached;
        public string MainForm_Simulate_Debug_Info => loc.strings.MainForm_Simulate_Debug_Info;
        public string MainForm_Simulate_Game_ApplyRuleButton => loc.strings.MainForm_Simulate_Game_ApplyRuleButton;
        public string MainForm_Simulate_Game_KnownCards => loc.strings.MainForm_Simulate_Game_KnownCards;
        public string MainForm_Simulate_Game_SkipRuleButton => loc.strings.MainForm_Simulate_Game_SkipRuleButton;
        public string MainForm_Simulate_Game_SpecialRule => loc.strings.MainForm_Simulate_Game_SpecialRule;
        public string MainForm_Simulate_Open_Hint => loc.strings.MainForm_Simulate_Open_Hint;
        public string MainForm_Simulate_Random_Info => loc.strings.MainForm_Simulate_Random_Info;
        public string MainForm_Simulate_ResetButton => loc.strings.MainForm_Simulate_ResetButton;
        public string MainForm_Simulate_Roulette_Rule1 => loc.strings.MainForm_Simulate_Roulette_Rule1;
        public string MainForm_Simulate_Roulette_Rule2 => loc.strings.MainForm_Simulate_Roulette_Rule2;
        public string MainForm_Simulate_Roulette_Rule3 => loc.strings.MainForm_Simulate_Roulette_Rule3;
        public string MainForm_Simulate_Roulette_Rule4 => loc.strings.MainForm_Simulate_Roulette_Rule4;
        public string MainForm_Simulate_RuleList => loc.strings.MainForm_Simulate_RuleList;
        public string MainForm_Simulate_WinChance => loc.strings.MainForm_Simulate_WinChance;
        public string MainForm_Dynamic_Simulate_ChangeBlueHint => loc.strings.MainForm_Dynamic_Simulate_ChangeBlueHint;
        public string MainForm_Dynamic_Simulate_SwapRuleButton => loc.strings.MainForm_Dynamic_Simulate_SwapRuleButton;
        public string MainForm_Dynamic_Simulate_BlueStartButton => loc.strings.MainForm_Dynamic_Simulate_BlueStartButton;

        public PageSimulateViewModel()
        {
            // design time only
        }

        public PageSimulateViewModel(MainWindowViewModel mainVM)
        {
            MainWindow = mainVM;

            for (int idx = 0; idx < 9; idx++)
            {
                var boardVM = new BoardCardVM(idx);
                boardVM.Card.OwnerObject = this;
                boardVM.Card.IsUsingImageBig = PlayerSettingsDB.Get().useSmallIcons == false;
                BoardCards.Add(boardVM);
            }

            PlayerDeck.CanUseBigIcons = false;
            for (int idx = 0; idx < 5; idx++)
            {
                PlayerDeck.Cards.Add(new CardViewModel());
            }

            SpecialRules.OwnerVM = this;
            SpecialRules.GameModel = mainVM.GameModel;

            mainVM.GameModel.OnNpcChanged += GameModel_OnNpcChanged;
            mainVM.GameModel.OnDeckChanged += GameModel_OnDeckChanged;
            mainVM.GameModel.OnGameStateChanged += GameModel_OnGameStateChanged;
            mainVM.GameModel.OnCachedWinChanceChanged += GameModel_OnCachedWinChanceChanged;
            mainVM.GameModel.OnSetupChanged += GameModel_OnSetupChanged;
            GameModel_OnNpcChanged(mainVM.GameModel.Npc);
            GameModel_OnDeckChanged(mainVM.GameModel.PlayerDeck);
            GameModel_OnSetupChanged(mainVM.GameModel);
            GameModel_OnGameStateChanged(mainVM.GameModel.GameState, null);

            CommandReset = new RelayCommand<object>((_) => MainWindow.GameModel.GameReset());
            CommandBlueFirst = new RelayCommand<object>((_) => MainWindow.GameModel.GameStartBlue(), (_) => canStartWithBlue && SpecialRules.ActiveRule == SimulateRulesViewModel.Rule.None);
            CommandRedUndo = new RelayCommand<object>((_) => { waitingGameResult = true; MainWindow.GameModel.GameUndoRed(); }, (_) => MainWindow.GameModel.UndoStateRed.Count > 0);

            PlayerDeck.SetCustomSelect(new RelayCommand<CardViewModel>((card) => MainWindow.GameModel.SetGameForcedBlueCard(card.CardModel.cardOb)));
            RedKnownCards.CanReceiveDropCards = false;
            RedUnknownCards.CanReceiveDropCards = false;

            MainWindow.PageInfo.OnSettingsChanged += PageInfo_OnSettingsChanged;
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();

            UpdateRules();
            UpdateCachedText();
        }

        public void GameModel_OnGameStateChanged(TriadGameData state, TriadGameModel.Move move)
        {
            var modelProxyDB = ModelProxyDB.Get();
            cachedLastState = state;
            cachedLastMove = move;

            // player deck
            for (int idx = 0; idx < PlayerDeck.Cards.Count; idx++)
            {
                var cardVM = PlayerDeck.Cards[idx];
                var deckCardOb = state.deckBlue.GetCard(idx);

                cardVM.IsPreview = (move != null) && (move.Card == deckCardOb);
                cardVM.CardModel = (!cardVM.IsPreview && state.deckBlue.IsPlaced(idx)) ? null : modelProxyDB.GetCardProxy(deckCardOb);
            }

            var hasPendingInteractiveRules = VerifyInteractiveRules(state);
            if (!hasPendingInteractiveRules)
            {
                // board
                for (int idx = 0; idx < BoardCards.Count; idx++)
                {
                    var cardVM = BoardCards[idx].Card;
                    cardVM.Assign(state.board[idx]);
                    cardVM.IsHighlighted = false;
                }

                // red decks
                int numKnownRed = state.deckRed.deck.knownCards.Count;
                SyncDeckVM(RedKnownCards, state.deckRed.deck.knownCards);
                for (int idx = 0; idx < numKnownRed; idx++)
                {
                    var isPlaced = state.deckRed.IsPlaced(idx);
                    RedKnownCards.Cards[idx].IsShowingDetails = !isPlaced;
                    if (isPlaced)
                    {
                        RedKnownCards.Cards[idx].CardModel = null;
                    }
                }

                NumUnknownToPlace = 5 - numKnownRed - state.deckRed.numUnknownPlaced;
                if (NumUnknownToPlace > 0)
                {
                    SyncDeckVM(RedUnknownCards, state.deckRed.deck.unknownCardPool);
                    for (int idx = 0; idx < state.deckRed.deck.unknownCardPool.Count; idx++)
                    {
                        var isPlaced = state.deckRed.IsPlaced(numKnownRed + idx);
                        RedUnknownCards.Cards[idx].IsShowingDetails = !isPlaced;
                        if (isPlaced)
                        {
                            RedUnknownCards.Cards[idx].CardModel = null;
                        }
                    }
                }
            }

            // current move
            if (move != null)
            {
                BoardCards[move.BoardIdx].Card.IsHighlighted = true;
                WinChance.SetValue(move.WinChance);

                // make sure tat undo button is updated, lags a bit on first move
                (CommandRedUndo as RelayCommand<object>).RaiseCanExecuteChanged();
            }
            else if (state.numCardsPlaced == 0)
            {
                WinChance.SetValue(MainWindow.GameModel.CachedWinChance);
            }
            else
            {
                WinChance.SetInvalid();
            }

            // other
            CanStartWithBlue = state.numCardsPlaced == 0;
            UpdateCachedText();
        }

        private void GameModel_OnCachedWinChanceChanged(TriadGameModel model)
        {
            if (model.GameState != null && model.GameState.numCardsPlaced == 0)
            {
                WinChance.SetValue(model.CachedWinChance);
            }
        }

        private void GameModel_OnSetupChanged(TriadGameModel model)
        {
            UpdateRules();

            CanSelectBlue = (model.Session.specialRules & ETriadGameSpecialMod.BlueCardSelection) != ETriadGameSpecialMod.None;
        }

        private void GameModel_OnDeckChanged(TriadDeck deckOb)
        {
            SyncDeckVM(PlayerDeck, deckOb.knownCards);
        }

        private void GameModel_OnNpcChanged(TriadNpc npcOb)
        {
            SyncDeckVM(RedKnownCards, npcOb.Deck.knownCards);
            SyncDeckVM(RedUnknownCards, npcOb.Deck.unknownCardPool);
        }

        private void PageInfo_OnSettingsChanged(object sender, SettingsEventArgs e)
        {
            if (e.Type == SettingsEventArgs.Setting.UseSmallIcons)
            {
                // updating just IsUsingImageBig is causing weird artifacts, rebuild entire VM 
                // this isn't changing often (or hopefully: at all)

                BoardCards.Clear();
                for (int idx = 0; idx < 9; idx++)
                {
                    var boardVM = new BoardCardVM(idx);
                    boardVM.Card.OwnerObject = this;
                    boardVM.Card.IsUsingImageBig = PlayerSettingsDB.Get().useSmallIcons == false;
                    BoardCards.Add(boardVM);
                }

                GameModel_OnGameStateChanged(cachedLastState, cachedLastMove);
            }
        }

        private void SyncDeckVM(DeckViewModel deckVM, List<TriadCard> cards)
        {
            while (deckVM.Cards.Count > cards.Count)
            {
                deckVM.Cards.RemoveAt(deckVM.Cards.Count - 1);
            }

            while (deckVM.Cards.Count < cards.Count)
            {
                deckVM.Cards.Add(new CardViewModel());
            }

            var modelProxyDB = ModelProxyDB.Get();
            for (int idx = 0; idx < cards.Count; idx++)
            {
                deckVM.Cards[idx].CardModel = modelProxyDB.GetCardProxy(cards[idx]);
            }
        }

        private bool VerifyInteractiveRules(TriadGameData state)
        {
            var pendingUIRules = (state.numCardsPlaced == 0) ? (MainWindow.GameModel.Session.specialRules & ~state.resolvedSpecial) : ETriadGameSpecialMod.None;
            if (pendingUIRules != ETriadGameSpecialMod.None)
            {
                if ((pendingUIRules & ETriadGameSpecialMod.RandomizeRule) != ETriadGameSpecialMod.None)
                {
                    SpecialRules.RequestRuleRoulette();
                }
                else if ((pendingUIRules & ETriadGameSpecialMod.RandomizeBlueDeck) != ETriadGameSpecialMod.None)
                {
                    SpecialRules.RequestRuleRandom();
                }
                else if ((pendingUIRules & ETriadGameSpecialMod.SwapCards) != ETriadGameSpecialMod.None)
                {
                    SpecialRules.RequestRuleSwap();
                }
                else if ((pendingUIRules & ETriadGameSpecialMod.SelectVisible3) != ETriadGameSpecialMod.None)
                {
                    SpecialRules.RequestRuleXOpen(3);
                }
                else if ((pendingUIRules & ETriadGameSpecialMod.SelectVisible5) != ETriadGameSpecialMod.None)
                {
                    SpecialRules.RequestRuleXOpen(5);
                }
            }

            OnPropertyChanged("SpecialRuleSwitcherIdx");
            OnPropertyChanged("SpecialRuleSwitcherBoardIdx");

            return SpecialRules.ActiveRule != SimulateRulesViewModel.Rule.None;
        }

        private void UpdateRules()
        {
            string desc = "";
            foreach (var rule in MainWindow.GameModel.Session.modifiers)
            {
                string ruleName = rule.GetLocalizedName();
                if (ruleName.Length > 0)
                {
                    if (desc.Length > 0) { desc += ", "; }
                    desc += ruleName;
                }
            }

            DescRules = (desc.Length > 0) ? desc : loc.strings.MainForm_Dynamic_RuleListEmpty;
        }

        private void UpdateCachedText()
        {
            string newGameResult = null;
            switch (MainWindow.GameModel.GameState.state)
            {
                case ETriadGameState.BlueWins: newGameResult = loc.strings.MainForm_Dynamic_Simulate_EndGame_BlueWin; break;
                case ETriadGameState.BlueDraw: newGameResult = loc.strings.MainForm_Dynamic_Simulate_EndGame_BlueDraw; break;
                case ETriadGameState.BlueLost: newGameResult = loc.strings.MainForm_Dynamic_Simulate_EndGame_BlueLost; break;
                default:
                    if (waitingGameResult)
                    {
                        waitingGameResult = false;
                        newGameResult = "...";
                    }
                    break;
            }

            descGameResult = newGameResult;
            OnPropertyChanged("DescChanceAndResult");

            bool hasLastRedReminder = false;
            if (MainWindow.GameModel.GameState.numCardsPlaced == (MainWindow.GameModel.GameState.board.Length - 1) &&
                MainWindow.GameModel.GameState.state == ETriadGameState.InProgressRed)
            {
                foreach (TriadGameModifier mod in MainWindow.GameModel.Session.modifiers)
                {
                    hasLastRedReminder = hasLastRedReminder || mod.HasLastRedReminder();
                }
            }

            HasGameHint = hasLastRedReminder;
            DescUndoButton = (MainWindow.GameModel.GameState.numCardsPlaced == 0) ? loc.strings.MainForm_Dynamic_Simulate_RedStartButton :
                    loc.strings.MainForm_Simulate_UndoRedMoveButton;
        }

        public bool IsCardDropAllowed(CardViewModel sourceCard, object sourceContainer)
        {
            return true;
        }

        public void OnCardDragEnter(CardViewModel sourceCard, CardViewModel destCard)
        {
            destCard.DragImage = sourceCard.CardImage;
            destCard.CardDragMode = ECardDragMode.DragIn;
        }

        public void OnCardDragLeave(CardViewModel sourceCard, CardViewModel destCard)
        {
            destCard.CardDragMode = ECardDragMode.None;
        }

        public void OnCardDrop(CardViewModel sourceCard, CardViewModel destCard, object sourceContainer)
        {
            destCard.CardDragMode = ECardDragMode.None;

            var sourceDeck = sourceContainer as DeckViewModel;
            if (sourceDeck == RedKnownCards || sourceDeck == RedUnknownCards)
            {
                MainWindow.GameModel.SetGameRedCard(sourceCard.CardModel.cardOb, destCard.OwnerIndex);
            }
        }
    }
}
