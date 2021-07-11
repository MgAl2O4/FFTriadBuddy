using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;

namespace FFTriadBuddy.UI
{
    // helper class for translating between tool's data objects and Models used by UI
    // technically, it's supposed to be ViewModel layer
    public class ModelProxyDB
    {
        private List<CardModelProxy> cards = new List<CardModelProxy>();
        public List<CardModelProxy> Cards => cards;

        private BulkObservableCollection<CardModelProxy> ownedCards = new BulkObservableCollection<CardModelProxy>();
        public BulkObservableCollection<CardModelProxy> OwnedCards => ownedCards;

        private List<NpcModelProxy> npcs = new List<NpcModelProxy>();
        public List<NpcModelProxy> Npcs => npcs;

        private List<RuleModelProxy> rules = new List<RuleModelProxy>();
        public List<RuleModelProxy> Rules => rules;

        private List<TournamentModelProxy> tournaments = new List<TournamentModelProxy>();
        public List<TournamentModelProxy> Tournaments => tournaments;

        public event Action<CardModelProxy> OnCardOwnerChanged;

        private static ModelProxyDB instance = new ModelProxyDB();
        public static ModelProxyDB Get() { return instance; }

        public void Load()
        {
            LoadCards();
            LoadOwnedCards();
            LoadNpc();
            LoadRules();
            LoadTournaments();

            PlayerSettingsDB.Get().OnUpdated += ModelProxyDB_OnUpdated;
            UpdateCompletedNpcs();

            LocalizationDB.OnLanguageChanged += LocalizationDB_OnLanguageChanged;
        }

        private void LoadCards()
        {
            TriadCardDB cardDB = TriadCardDB.Get();
            cards.Clear();

            for (int idx = 0; idx < cardDB.cards.Count; idx++)
            {
                var cardEntry = cardDB.cards[idx];
                if (cardEntry != null)
                {
                    cards.Add(new CardModelProxy(cardEntry));
                }
            }
        }

        private void LoadOwnedCards()
        {
            ownedCards.SuspendNotifies();
            ownedCards.Clear();

            var settingsDB = PlayerSettingsDB.Get();
            foreach (var card in settingsDB.ownedCards)
            {
                var cardProxy = cards.Find(x => x.cardOb.Id == card.Id);
                if (cardProxy != null)
                {
                    cardProxy.IsOwned = true;
                    ownedCards.Add(cardProxy);
                }
            }

            ownedCards.ResumeNotifies();
        }

        private void LoadNpc()
        {
            TriadNpcDB npcDB = TriadNpcDB.Get();
            npcs.Clear();

            for (int idx = 0; idx < npcDB.npcs.Count; idx++)
            {
                var npcEntry = npcDB.npcs[idx];
                if (npcEntry != null)
                {
                    npcs.Add(new NpcModelProxy(npcEntry));
                }
            }
        }

        private void LoadRules()
        {
            TriadGameModifierDB modDB = TriadGameModifierDB.Get();
            rules.Clear();

            for (int idx = 0; idx < modDB.mods.Count; idx++)
            {
                var modEntry = modDB.mods[idx];
                if (modEntry != null)
                {
                    rules.Add(new RuleModelProxy(modEntry));
                }
            }

            var view = CollectionViewSource.GetDefaultView(rules);
            if (view.SortDescriptions.Count == 0)
            {
                view.SortDescriptions.Add(new SortDescription());
            }
        }

        private void LoadTournaments()
        {
            TriadTournamentDB tournamentDB = TriadTournamentDB.Get();
            tournaments.Clear();

            for (int idx = 0; idx < tournamentDB.tournaments.Count; idx++)
            {
                var tourEntry = tournamentDB.tournaments[idx];
                if (tourEntry != null)
                {
                    tournaments.Add(new TournamentModelProxy(tourEntry));
                }
            }

            var view = CollectionViewSource.GetDefaultView(tournaments);
            if (view.SortDescriptions.Count == 0)
            {
                view.SortDescriptions.Add(new SortDescription());
            }
        }

        private void ModelProxyDB_OnUpdated(bool bCards, bool bNpcs, bool bDecks)
        {
            if (bCards)
            {
                LoadOwnedCards();
            }
        }

        private void LocalizationDB_OnLanguageChanged()
        {
            foreach (var npc in npcs)
            {
                npc.RefreshLocalization();
            }

            foreach (var tournament in tournaments)
            {
                tournament.RefreshLocalization();
            }

            foreach (var rule in rules)
            {
                rule.RefreshLocalization();
            }

            CollectionViewSource.GetDefaultView(npcs).Refresh();
            CollectionViewSource.GetDefaultView(cards).Refresh();
            CollectionViewSource.GetDefaultView(rules).Refresh();
            CollectionViewSource.GetDefaultView(tournaments).Refresh();
        }

        public void UpdateOwnedCard(CardModelProxy cardProxy)
        {
            if (!ownedCards.IsNotifySuspended)
            {
                var settingsDB = PlayerSettingsDB.Get();
                var hasChanges = false;

                if (cardProxy.IsOwned && !OwnedCards.Contains(cardProxy))
                {
                    //Logger.WriteLine("Adding owned card: {0}", cardProxy.cardOb.Name.GetCodeName());
                    OwnedCards.Add(cardProxy);
                    settingsDB.ownedCards.Add(cardProxy.cardOb);
                    hasChanges = true;
                }
                else if (!cardProxy.IsOwned && OwnedCards.Contains(cardProxy))
                {
                    //Logger.WriteLine("Removing owned card: {0}", cardProxy.cardOb.Name.GetCodeName());
                    OwnedCards.Remove(cardProxy);
                    settingsDB.ownedCards.Remove(cardProxy.cardOb);
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    settingsDB.MarkDirty();
                    UpdateCompletedNpcs();

                    OnCardOwnerChanged?.Invoke(cardProxy);
                }
            }
        }

        private void UpdateCompletedNpcs()
        {
            var settingsDB = PlayerSettingsDB.Get();
            foreach (var npc in npcs)
            {
                var notOwnedReward = npc.npcOb.Rewards.Find(x => !settingsDB.ownedCards.Contains(x));
                npc.IsCompleted = notOwnedReward == null;
                npc.UpdateCachedText();
            }
        }

        public NpcModelProxy GetNpcProxy(TriadNpc npcOb)
        {
            return Npcs.Find(x => x.npcOb == npcOb);
        }

        public CardModelProxy GetCardProxy(TriadCard cardOb)
        {
            return Cards.Find(x => x.cardOb == cardOb);
        }
    }
}
