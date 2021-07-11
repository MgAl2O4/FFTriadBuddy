using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class SetupFavDeckViewModel : LocalizedViewModel
    {
        private BulkObservableCollection<SolvableDeckViewModel> favDecks = new BulkObservableCollection<SolvableDeckViewModel>();
        public BulkObservableCollection<SolvableDeckViewModel> FavDecks => favDecks;

        public TriadGameModel gameModel;
        public DeckViewModel activeDeck;

        public ICommand CommandFavUse { get; private set; }
        public ICommand CommandFavAssign { get; private set; }
        public ICommand CommandFavCreate { get; private set; }
        public ICommand CommandFavEdit { get; private set; }
        public ICommand CommandFavRemove { get; private set; }

        public SetupFavDeckViewModel()
        {
            CommandFavUse = new RelayCommand<SolvableDeckViewModel>(CommandFavUseFunc);
            CommandFavAssign = new RelayCommand<SolvableDeckViewModel>((favDeck) => AssignFavDeckFrom(favDeck, activeDeck));
            CommandFavEdit = new RelayCommand<SolvableDeckViewModel>(CommandFavEditFunc);
            CommandFavRemove = new RelayCommand<SolvableDeckViewModel>(CommandFavRemoveFunc);
            CommandFavCreate = new RelayCommand<object>(CommandFavCreateFunc);
        }

        public void Initialize()
        {
            var settingsDB = PlayerSettingsDB.Get();
            var modelProxyDB = ModelProxyDB.Get();

            favDecks.SuspendNotifies();

            foreach (var favDeckOb in settingsDB.favDecks)
            {
                var deck = new CardCollectionViewModel() { Name = favDeckOb.Name };
                foreach (var cardOb in favDeckOb.knownCards)
                {
                    var cardVM = new CardViewModel() { CardModel = modelProxyDB.GetCardProxy(cardOb) };
                    deck.Cards.Add(cardVM);
                }

                var favDeck = new SolvableDeckViewModel() { Deck = deck };
                favDecks.Add(favDeck);

                favDeck.RefreshSolver(gameModel, favDeckOb);
            }

            favDecks.ResumeNotifies();
        }

        public override void RefreshLocalization()
        {
            // ignore base, there aren't any strings to localize directly in here
            // base.RefreshLocalization();

            foreach (var favDeck in favDecks)
            {
                favDeck.RefreshLocalization();
            }
        }

        private void CommandFavUseFunc(SolvableDeckViewModel favDeck)
        {
            if (activeDeck.Cards.Count == favDeck.Deck.Cards.Count)
            {
                activeDeck.Cards.SuspendNotifies();
                for (int idx = 0; idx < activeDeck.Cards.Count; idx++)
                {
                    activeDeck.Cards[idx].CardModel = favDeck.Deck.Cards[idx].CardModel;
                    activeDeck.Cards[idx].IsShowingLock = false;
                }

                activeDeck.Cards.ResumeNotifies();
                activeDeck.ForceCardsUpdate();
            }
        }

        private void AssignFavDeckFrom(SolvableDeckViewModel favDeck, DeckViewModel sourceDeck)
        {
            if (sourceDeck.Cards.Count == favDeck.Deck.Cards.Count)
            {
                favDeck.Deck.Cards.SuspendNotifies();
                var cards = new List<TriadCard>();

                for (int idx = 0; idx < sourceDeck.Cards.Count; idx++)
                {
                    var cardProxy = sourceDeck.Cards[idx].CardModel;
                    favDeck.Deck.Cards[idx].CardModel = cardProxy;
                    cards.Add(cardProxy.cardOb);
                }

                favDeck.Deck.Cards.ResumeNotifies();

                int slotIdx = favDecks.IndexOf(favDeck);
                var namedDeck = new TriadDeckNamed(new TriadDeck(cards)) { Name = favDeck.Deck.Name };
                PlayerSettingsDB.Get().UpdateFavDeck(slotIdx, namedDeck);

                favDeck.RefreshSolver(gameModel, namedDeck);
            }
        }

        private void CommandFavEditFunc(SolvableDeckViewModel favDeck)
        {
            var deckVM = new DeckViewModel() { Name = favDeck.Deck.Name };
            foreach (var card in favDeck.Deck.Cards)
            {
                deckVM.Cards.Add(new CardViewModel() { CardModel = card.CardModel });
            }

            var editVM = new FavDeckEditViewModel() { FavDeck = deckVM };
            var result = ViewModelServices.DialogWindow.ShowDialog(editVM);
            if (result ?? false)
            {
                favDeck.Deck.Name = deckVM.Name;
                AssignFavDeckFrom(favDeck, deckVM);
            }
        }

        private void CommandFavRemoveFunc(SolvableDeckViewModel favDeck)
        {
            int slotIdx = favDecks.IndexOf(favDeck);
            if (slotIdx >= 0)
            {
                var result = MessageBox.Show(loc.strings.FavDeckForm_Dynamic_RemoveMsg, loc.strings.App_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    PlayerSettingsDB.Get().UpdateFavDeck(slotIdx, null);

                    favDecks.RemoveAt(slotIdx);
                }
            }
        }

        private void CommandFavCreateFunc(object dummyParam)
        {
            var deck = new CardCollectionViewModel();

            for (int idx = 1; idx < 10000; idx++)
            {
                deck.Name = string.Format(loc.strings.FavDeckForm_Dynamic_AutoName, idx);

                bool foundMatch = false;
                foreach (var testFav in favDecks)
                {
                    foundMatch = testFav.Deck.Name == deck.Name;
                    if (foundMatch)
                    {
                        break;
                    }
                }

                if (!foundMatch)
                {
                    break;
                }
            }

            for (int idx = 0; idx < activeDeck.Cards.Count; idx++)
            {
                deck.Cards.Add(new CardViewModel());
            }

            var favDeck = new SolvableDeckViewModel() { Deck = deck };
            AssignFavDeckFrom(favDeck, activeDeck);

            favDecks.Add(favDeck);
        }
    }
}
