using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class PageCardsViewModel : LocalizedViewModel
    {
        public MainWindowViewModel MainWindow;

        public List<CardCollectionViewModel> GridViews { get; } = new List<CardCollectionViewModel>();
        public List<CardModelProxy> Cards => ModelProxyDB.Get().Cards;
        public int NumOwnedCards => ModelProxyDB.Get().OwnedCards.Count;

        private CardModelProxy foundItem = null;
        public CardModelProxy FoundItem { get => foundItem; set => PropertySetAndNotify(value, ref foundItem); }

        public BulkObservableCollection<ContextActionViewModel> ContextActions { get; } = new BulkObservableCollection<ContextActionViewModel>();

        public ICommand CommandToggleOwnedCard { get; private set; }
        public ICommand CommandSearchCard { get; private set; }
        public ICommand CommandBuildContextActions { get; private set; }

        public string MainForm_Cards_IconsTitle => loc.strings.MainForm_Cards_IconsTitle;
        public string MainForm_Cards_ListTitle => loc.strings.MainForm_Cards_ListTitle;
        public string MainForm_Cards_List_ColumnId => loc.strings.MainForm_Cards_List_ColumnId;
        public string MainForm_Cards_List_ColumnName => loc.strings.MainForm_Cards_List_ColumnName;
        public string MainForm_Cards_List_ColumnOwned => loc.strings.MainForm_Cards_List_ColumnOwned;
        public string MainForm_Cards_List_ColumnPower => loc.strings.MainForm_Cards_List_ColumnPower;
        public string MainForm_Cards_List_ColumnRarity => loc.strings.MainForm_Cards_List_ColumnRarity;
        public string MainForm_Cards_List_ColumnType => loc.strings.MainForm_Cards_List_ColumnType;
        public string MainForm_Cards_NumOwned => loc.strings.MainForm_Cards_NumOwned;
        public string MainForm_CtxMenu_FindCard => loc.strings.MainForm_CtxMenu_FindCard;

        public PageCardsViewModel()
        {
            // design time only
        }

        public PageCardsViewModel(MainWindowViewModel mainVM)
        {
            MainWindow = mainVM;
            CreateGridViews();

            ModelProxyDB.Get().OwnedCards.CollectionChanged += (s, e) => OnPropertyChanged("NumOwnedCards");
            PlayerSettingsDB.Get().OnUpdated += OnSettingsUpdated;

            CommandToggleOwnedCard = new RelayCommand<CardModelProxy>(CommandToggleOwnedCardFunc);
            CommandSearchCard = new RelayCommand<string>(CommandSearchCardFunc);
            CommandBuildContextActions = new RelayCommand<CardModelProxy>(CommandBuildContextActionsFunc);
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();
            ContextActions.Clear();
        }

        private void CreateGridViews()
        {
            ModelProxyDB modelProxyDB = ModelProxyDB.Get();
            int gridSize = 30;

            var sortedList = new List<CardModelProxy>();
            sortedList.AddRange(modelProxyDB.Cards);
            sortedList.Sort((a, b) => a.GameSortOrder.CompareTo(b.GameSortOrder));

            CardCollectionViewModel currentGridVM = null;
            int currentSortGroup = -1;
            for (int idx = 0; idx < sortedList.Count; idx++)
            {
                var cardProxy = sortedList[idx];
                bool createNewGrid =
                    (currentGridVM == null) ||
                    (currentSortGroup != cardProxy.GameSortGroup) ||
                    (currentGridVM.Cards.Count >= gridSize);

                if (createNewGrid)
                {
                    currentGridVM = new CardCollectionViewModel() { Name = string.Format("-- {0} --", GridViews.Count + 1) };
                    GridViews.Add(currentGridVM);
                }

                currentGridVM.Cards.Add(new CardViewModel() { CardModel = cardProxy });
                currentSortGroup = cardProxy.GameSortGroup;
            }
        }

        private void OnSettingsUpdated(bool bCards, bool bNpcs, bool bDecks)
        {
            if (bCards)
            {
                GridViews.Clear();
                CreateGridViews();

                CollectionViewSource.GetDefaultView(GridViews).Refresh();
            }
        }

        private void CommandToggleOwnedCardFunc(CardModelProxy cardProxy)
        {
            cardProxy.IsOwned = !cardProxy.IsOwned;
        }

        private void CommandSearchCardFunc(string text)
        {
            FoundItem = Cards.Find(x => x.NameLocalized.StartsWith(text, StringComparison.OrdinalIgnoreCase));
        }

        private void CommandBuildContextActionsFunc(CardModelProxy cardProxy)
        {
            ContextActions.SuspendNotifies();

            const int numDefaultItems = 3;
            if (ContextActions.Count == 0)
            {
                ContextActions.Add(new ContextActionViewModel() { Name = loc.strings.MainForm_CtxMenu_CardInfo_FindOnline, Command = new RelayCommand<object>(x => FindCardOnline(x as CardModelProxy)) });
                ContextActions.Add(new ContextActionViewModel() { IsSeparator = true });
                ContextActions.Add(new ContextActionViewModel() { Name = loc.strings.MainForm_CtxMenu_CardInfo_NpcReward, Command = new RelayCommand<object>(x => { }, x => false) });
            }
            else if (ContextActions.Count > numDefaultItems)
            {
                while (ContextActions.Count > numDefaultItems)
                {
                    ContextActions.RemoveAt(numDefaultItems);
                }
            }

            if (cardProxy != null)
            {
                List<NpcModelProxy> matchingNpcs = ModelProxyDB.Get().Npcs.FindAll(x => x.npcOb.Rewards.Contains(cardProxy.cardOb));
                if (matchingNpcs.Count > 0)
                {
                    foreach (var npc in matchingNpcs)
                    {
                        ContextActions.Add(new ContextActionViewModel() { Name = npc.NameLocalized, Command = new RelayCommand<object>(x => SelectCardNpc(npc)) });
                    }
                }
            }

            if (ContextActions.Count == numDefaultItems)
            {
                ContextActions.Add(new ContextActionViewModel() { Name = loc.strings.MainForm_Dynamic_RuleListEmpty, Command = new RelayCommand<object>(x => { }, x => false) });
            }

            ContextActions.ResumeNotifies();
        }

        private void FindCardOnline(CardModelProxy cardProxy)
        {
            Process.Start(new ProcessStartInfo("https://triad.raelys.com/cards/" + cardProxy.cardOb.Id) { UseShellExecute = true });
        }

        private void SelectCardNpc(NpcModelProxy npcProxy)
        {
            MainWindow.SwitchToPage(MainWindowViewModel.PageType.Npcs);
            MainWindow.PageNpcs.SelectNpc(npcProxy);
        }
    }
}
