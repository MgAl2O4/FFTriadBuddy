using System.Collections.Generic;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class SimulateRulesViewModel : BaseViewModel
    {
        public enum Rule
        {
            None,
            Roulette,
            Random,
            Swap,
            XOpen,
            Debug,
        }

        public PageSimulateViewModel OwnerVM;
        public TriadGameModel GameModel;

        public List<RuleModelProxy> Rules => ModelProxyDB.Get().Rules;

        private ETriadGameSpecialMod activeRuleMask;
        private Rule activeRule = Rule.None;
        public Rule ActiveRule { get => activeRule; set { if (value != activeRule) { PropertySetAndNotify(value, ref activeRule); } } }

        private RuleModelProxy roulette1;
        public RuleModelProxy Roulette1 { get => roulette1; set => PropertySetAndNotify(value, ref roulette1); }

        private RuleModelProxy roulette2;
        public RuleModelProxy Roulette2 { get => roulette2; set => PropertySetAndNotify(value, ref roulette2); }

        private RuleModelProxy roulette3;
        public RuleModelProxy Roulette3 { get => roulette3; set => PropertySetAndNotify(value, ref roulette3); }

        private RuleModelProxy roulette4;
        public RuleModelProxy Roulette4 { get => roulette4; set => PropertySetAndNotify(value, ref roulette4); }

        private int numRouletteChoices;
        public int NumRouletteChoices { get => numRouletteChoices; set => PropertySetAndNotify(value, ref numRouletteChoices); }

        public DeckViewModel OverrideBlueDeck { get; } = new DeckViewModel() { DeckOwner = ECardOwner.Blue };
        public DeckViewModel OverrideRedDeck { get; } = new DeckViewModel() { DeckOwner = ECardOwner.Red };

        private bool debugForcedCached;
        public bool DebugForceCached { get => debugForcedCached; set => PropertySetAndNotify(value, ref debugForcedCached); }

        private ICommand commandApply;
        public ICommand CommandApply { get => commandApply; set => PropertySetAndNotify(value, ref commandApply); }
        public ICommand CommandSkip { get; private set; }

        private Dictionary<Rule, ICommand> MapCommandApply = new Dictionary<Rule, ICommand>();
        private int NumXOpen = 5;

        public SimulateRulesViewModel()
        {
            MapCommandApply.Add(Rule.None, new RelayCommand<object>((_) => MarkRuleResolved()));
            MapCommandApply.Add(Rule.Debug, new RelayCommand<object>((_) => { ApplyRuleDebug(); MarkRuleResolved(); }));
            MapCommandApply.Add(Rule.Roulette, new RelayCommand<object>((_) => { ApplyRuleRoulette(); MarkRuleResolved(); }));
            MapCommandApply.Add(Rule.Random, new RelayCommand<object>((_) => { ApplyRuleRandom(); MarkRuleResolved(); }));
            MapCommandApply.Add(Rule.Swap, new RelayCommand<object>((_) => { ApplyRuleSwap(); MarkRuleResolved(); }, (_) => CanApplyRuleSwap()));
            MapCommandApply.Add(Rule.XOpen, new RelayCommand<object>((_) => { ApplyRuleXOpen(); MarkRuleResolved(); }, (_) => CanApplyRuleXOpen()));

            CommandSkip = MapCommandApply[Rule.None];
            CommandApply = MapCommandApply[Rule.None];
        }

        public void RequestRuleDebug()
        {
            activeRuleMask = ETriadGameSpecialMod.None;
            ActiveRule = Rule.Debug;
            CommandApply = MapCommandApply[ActiveRule];

            OwnerVM.MainWindow.SwitchToPage(MainWindowViewModel.PageType.Simulate);
            OwnerVM.GameModel_OnGameStateChanged(GameModel.GameState, null);
        }

        private void ApplyRuleDebug()
        {
            OwnerVM.MainWindow.PageScreenshot.RequestDebugScreenshot(DebugForceCached);
            OwnerVM.MainWindow.SwitchToPage(MainWindowViewModel.PageType.Screenshot);
        }

        public void RequestRuleRoulette()
        {
            int numRules = 0;
            foreach (var mod in GameModel.Session.modifiers)
            {
                var rouletteModOb = mod as TriadGameModifierRoulette;
                if (rouletteModOb != null)
                {
                    rouletteModOb.SetRuleInstance(null);
                    numRules++;
                }
            }

            var defaultRule = Rules.Find(x => x.modOb is TriadGameModifierNone);

            NumRouletteChoices = numRules;
            Roulette1 = defaultRule;
            Roulette2 = defaultRule;
            Roulette3 = defaultRule;
            Roulette4 = defaultRule;

            activeRuleMask = ETriadGameSpecialMod.RandomizeRule;
            ActiveRule = Rule.Roulette;
            CommandApply = MapCommandApply[ActiveRule];
        }

        private void ApplyRuleRoulette()
        {
            int readIdx = 0;
            foreach (var mod in GameModel.Session.modifiers)
            {
                var rouletteModOb = mod as TriadGameModifierRoulette;
                if (rouletteModOb != null)
                {
                    switch (readIdx)
                    {
                        case 0: rouletteModOb.SetRuleInstance(Roulette1.modOb); break;
                        case 1: rouletteModOb.SetRuleInstance(Roulette2.modOb); break;
                        case 2: rouletteModOb.SetRuleInstance(Roulette3.modOb); break;
                        case 3: rouletteModOb.SetRuleInstance(Roulette4.modOb); break;
                        default: break;
                    }

                    readIdx++;
                }
            }

            GameModel.GameRouletteApplied();
        }

        public void RequestRuleRandom()
        {
            var deckBlueOb = GameModel.GameState.deckBlue.deck;
            SyncDeckVM(OverrideBlueDeck, deckBlueOb.knownCards, deckBlueOb.unknownCardPool);

            activeRuleMask = ETriadGameSpecialMod.RandomizeBlueDeck;
            ActiveRule = Rule.Random;
            CommandApply = MapCommandApply[ActiveRule];
        }

        private void ApplyRuleRandom()
        {
            var updatedCards = new List<TriadCard>();
            foreach (var card in OverrideBlueDeck.Cards)
            {
                updatedCards.Add(card.CardModel.cardOb);
            }

            GameModel.GameState.deckBlue = new TriadDeckInstanceManual(new TriadDeck(updatedCards));

            GameModel.GameState.bDebugRules = true;
            TriadGameModifierRandom.StaticRandomized(GameModel.GameState);
            GameModel.GameState.bDebugRules = false;
        }

        public void RequestRuleSwap()
        {
            var deckBlueOb = GameModel.GameState.deckBlue.deck;
            SyncDeckVM(OverrideBlueDeck, deckBlueOb.knownCards, deckBlueOb.unknownCardPool);
            OverrideBlueDeck.NumToSelect = 1;

            var deckRedOb = GameModel.GameState.deckRed.deck;
            SyncDeckVM(OverrideRedDeck, deckRedOb.knownCards, deckRedOb.unknownCardPool);
            OverrideRedDeck.NumToSelect = 1;

            GameModel.OnDeckChanged += OnSwapDeckChanged;

            activeRuleMask = ETriadGameSpecialMod.SwapCards;
            ActiveRule = Rule.Swap;
            CommandApply = MapCommandApply[ActiveRule];
        }

        private bool CanApplyRuleSwap()
        {
            return (OverrideBlueDeck.NumSelected > 0) && (OverrideRedDeck.NumSelected > 0);
        }

        private void ApplyRuleSwap()
        {
            GameModel.OnDeckChanged -= OnSwapDeckChanged;

            int redIdx = OverrideRedDeck.SelectedIndices[0];
            int blueIdx = OverrideBlueDeck.SelectedIndices[0];

            var redCardOb = OverrideRedDeck.Cards[redIdx].CardModel.cardOb;
            var blueCardOb = OverrideBlueDeck.Cards[blueIdx].CardModel.cardOb;

            TriadGameModifierSwap.StaticSwapCards(GameModel.GameState, blueCardOb, blueIdx, redCardOb, redIdx);
        }

        private void OnSwapDeckChanged(TriadDeck deckBlueOb)
        {
            SyncDeckVM(OverrideBlueDeck, deckBlueOb.knownCards, deckBlueOb.unknownCardPool);
            OverrideBlueDeck.NumToSelect = 1;
        }

        public void RequestRuleXOpen(int count)
        {
            activeRuleMask = ETriadGameSpecialMod.SelectVisible3 | ETriadGameSpecialMod.SelectVisible5;
            NumXOpen = count;

            // ignore when it's all open vs 5 cards total (e.g. tutorial npc)
            var deckOb = GameModel.GameState.deckRed.deck;
            int numCardsTotal = deckOb.knownCards.Count + deckOb.unknownCardPool.Count;
            if (numCardsTotal == count)
            {
                NumXOpen = 0;
                MarkRuleResolved();
                return;
            }

            SyncDeckVM(OverrideRedDeck, deckOb.knownCards, deckOb.unknownCardPool);
            OverrideRedDeck.NumToSelect = count;

            if (NumXOpen == 5)
            {
                for (int idx = 0; idx < deckOb.knownCards.Count; idx++)
                {
                    OverrideRedDeck.CommandSelect.Execute(OverrideRedDeck.Cards[idx]);
                }
            }

            ActiveRule = Rule.XOpen;
            CommandApply = MapCommandApply[ActiveRule];
        }

        private bool CanApplyRuleXOpen()
        {
            return OverrideRedDeck.NumSelected == NumXOpen;
        }

        private void ApplyRuleXOpen()
        {
            TriadGameModifierAllOpen.StaticMakeKnown(GameModel.GameState, OverrideRedDeck.SelectedIndices);
        }

        private void MarkRuleResolved()
        {
            ActiveRule = Rule.None;
            GameModel.ResolveSpecialRule(activeRuleMask);
        }

        private void SyncDeckVM(DeckViewModel deckVM, List<TriadCard> cardsA, List<TriadCard> cardsB)
        {
            int numCards = cardsA.Count + cardsB.Count;
            while (deckVM.Cards.Count > numCards)
            {
                deckVM.Cards.RemoveAt(deckVM.Cards.Count - 1);
            }

            while (deckVM.Cards.Count < numCards)
            {
                deckVM.Cards.Add(new CardViewModel());
            }

            var modelProxyDB = ModelProxyDB.Get();
            for (int idx = 0; idx < cardsA.Count; idx++)
            {
                deckVM.Cards[idx].CardModel = modelProxyDB.GetCardProxy(cardsA[idx]);
            }
            for (int idx = 0; idx < cardsB.Count; idx++)
            {
                deckVM.Cards[idx + cardsA.Count].CardModel = modelProxyDB.GetCardProxy(cardsB[idx]);
            }

            deckVM.SelectedIndices.Clear();
            deckVM.NumToSelect = 0;

            foreach (var card in deckVM.Cards)
            {
                card.IsHighlighted = false;
            }
        }
    }
}
