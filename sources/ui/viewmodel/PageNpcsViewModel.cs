using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class PageNpcsViewModel : LocalizedViewModel
    {
        public MainWindowViewModel MainWindow;

        private CollectionViewSource unfilteredNpc = new CollectionViewSource();
        public ICollectionView Npcs => unfilteredNpc.View;

        private int remainingNpcs = 0;
        public int RemainingNpcWithReward { get => remainingNpcs; set => PropertySetAndNotify(value, ref remainingNpcs); }

        private NpcModelProxy foundItem = null;
        public NpcModelProxy FoundItem { get => foundItem; set => PropertySetAndNotify(value, ref foundItem); }

        public BulkObservableCollection<ContextActionViewModel> ContextActions { get; } = new BulkObservableCollection<ContextActionViewModel>();

        public ICommand CommandSearchNpc { get; private set; }
        public ICommand CommandBuildContextActions { get; private set; }

        public string MainForm_Npcs_List_ColumnCompleted => loc.strings.MainForm_Npcs_List_ColumnCompleted;
        public string MainForm_Npcs_List_ColumnLocation => loc.strings.MainForm_Npcs_List_ColumnLocation;
        public string MainForm_Npcs_List_ColumnName => loc.strings.MainForm_Npcs_List_ColumnName;
        public string MainForm_Npcs_List_ColumnPower => loc.strings.MainForm_Npcs_List_ColumnPower;
        public string MainForm_Npcs_List_ColumnReward => loc.strings.MainForm_Npcs_List_ColumnReward;
        public string MainForm_Npcs_List_ColumnRules => loc.strings.MainForm_Npcs_List_ColumnRules;
        public string MainForm_Npcs_NumKnown => loc.strings.MainForm_Npcs_NumKnown;
        public string MainForm_CtxMenu_FindNpc => loc.strings.MainForm_CtxMenu_FindNpc;

        public PageNpcsViewModel()
        {
            // design time only
        }

        public PageNpcsViewModel(MainWindowViewModel mainVM)
        {
            MainWindow = mainVM;

            var modelProxyDB = ModelProxyDB.Get();

            unfilteredNpc.Source = modelProxyDB.Npcs;
            modelProxyDB.OwnedCards.CollectionChanged += (s, e) => UpdateRemainingNpcs();
            UpdateRemainingNpcs();

            CommandSearchNpc = new RelayCommand<string>(CommandSearchNpcFunc);
            CommandBuildContextActions = new RelayCommand<NpcModelProxy>(CommandBuildContextActionsFunc);
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();
            ContextActions.Clear();
        }

        public void SelectNpc(NpcModelProxy npcProxy)
        {
            FoundItem = npcProxy;
        }

        private void UpdateRemainingNpcs()
        {
            RemainingNpcWithReward = ModelProxyDB.Get().Npcs.Count(x => !x.IsCompleted);
        }

        private void CommandSearchNpcFunc(string text)
        {
            FoundItem = ModelProxyDB.Get().Npcs.Find(x => x.NameLocalized.StartsWith(text, StringComparison.OrdinalIgnoreCase));
        }

        private void CommandBuildContextActionsFunc(NpcModelProxy npcProxy)
        {
            ContextActions.SuspendNotifies();

            const int numDefaultItems = 3;
            if (ContextActions.Count == 0)
            {
                ContextActions.Add(new ContextActionViewModel() { Name = loc.strings.MainForm_CtxMenu_SelectNpc_Select, Command = new RelayCommand<object>(x => SelectNpcToPlay(x as NpcModelProxy)) });
                ContextActions.Add(new ContextActionViewModel() { IsSeparator = true });
                ContextActions.Add(new ContextActionViewModel() { Name = loc.strings.MainForm_CtxMenu_SelectNpc_Rewards, Command = new RelayCommand<object>(x => { }, x => false) });
            }
            else if (ContextActions.Count > numDefaultItems)
            {
                while (ContextActions.Count > numDefaultItems)
                {
                    ContextActions.RemoveAt(numDefaultItems);
                }
            }

            if (npcProxy != null)
            {
                var modelDB = ModelProxyDB.Get();
                List<CardModelProxy> matchingCards = modelDB.Cards.FindAll(x => npcProxy.npcOb.Rewards.Contains(x.cardOb));
                if (matchingCards.Count > 0)
                {
                    foreach (var card in matchingCards)
                    {
                        ContextActions.Add(new ContextActionViewModel()
                        {
                            Name = card.NameLocalized,
                            IsCheckbox = true,
                            IsChecked = card.IsOwned,
                            Command = new RelayCommand<object>(x => card.IsOwned = !card.IsOwned)
                        });
                    }
                }
            }

            if (ContextActions.Count == numDefaultItems)
            {
                ContextActions.Add(new ContextActionViewModel() { Name = loc.strings.MainForm_Dynamic_RuleListEmpty, Command = new RelayCommand<object>(x => { }, x => false) });
            }

            ContextActions.ResumeNotifies();
        }

        private void SelectNpcToPlay(NpcModelProxy npcProxy)
        {
            MainWindow.GameModel.SetNpc(npcProxy?.npcOb);
        }
    }
}
