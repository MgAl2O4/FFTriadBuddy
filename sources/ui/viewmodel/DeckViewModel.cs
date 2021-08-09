using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace FFTriadBuddy.UI
{
    public class DeckViewModel : CardCollectionViewModel, ICardDragDropTarget
    {
        public class CardPickerParams
        {
            public CardViewModel cardVM;
            public CardModelProxy cardModel;
        }

        private CollectionViewSource cardPickerItems = new CollectionViewSource();
        public ICollectionView CardPickerItems => cardPickerItems.View;
        public event Action<DeckViewModel> OnCardsChanged;

        public bool CanReceiveDropCards = true;
        public int NumToSelect { get; set; } = 0;

        private List<int> selectedIndices = new List<int>();
        public List<int> SelectedIndices => selectedIndices;
        public int NumSelected => selectedIndices.Count;
        public event Action<DeckViewModel> OnSelectionChanged;

        private ECardOwner deckOwner = ECardOwner.None;
        public ECardOwner DeckOwner { get => deckOwner; set => PropertySetAndNotify(value, ref deckOwner); }

        private bool isShowingDetails = true;
        public bool IsShowingDetails { get => isShowingDetails; set => PropertySetAndNotify(value, ref isShowingDetails); }

        private bool useSmallIcons = false;
        private bool canUseBigIcons = true;
        public bool CanUseBigIcons { get => canUseBigIcons; set { PropertySetAndNotify(value, ref canUseBigIcons); UpdateAllCardIcons(); } }

        private bool isUsingOnlyOwnedCards = false;
        public bool IsUsingOnlyOwnedCards { get => isUsingOnlyOwnedCards; set { PropertySetAndNotify(value, ref isUsingOnlyOwnedCards); CardPickerItems.Refresh(); } }

        private CardViewModel selectedPreview = null;
        public CardViewModel SelectedPreview { get => selectedPreview; set => PropertySetAndNotify(value, ref selectedPreview); }

        public ICommand CommandToggleLock { get; private set; }
        public ICommand CommandPickCard { get; private set; }

        public string DeckCtrl_CtxMenu_LockForOptimization => loc.strings.DeckCtrl_CtxMenu_LockForOptimization;
        public string DeckCtrl_CtxMenu_PickCard => loc.strings.DeckCtrl_CtxMenu_PickCard;
        public string DeckCtrl_CtxMenu_UseOnlyOwned => loc.strings.DeckCtrl_CtxMenu_UseOnlyOwned;

        public DeckViewModel()
        {
            CommandSelect = new RelayCommand<CardViewModel>(CommandSelectFunc, (card) => (NumToSelect > 0) && (card != null));
            CommandPickCard = new RelayCommand<CardPickerParams>(CommandPickCardFunc);
            CommandToggleLock = new RelayCommand<CardViewModel>(CommandToggleLockFunc);

            Cards.CollectionChanged += Cards_CollectionChanged;

            cardPickerItems.Source = ModelProxyDB.Get().Cards;
            cardPickerItems.View.Filter += CardPickerItems_Filter;

            useSmallIcons = PlayerSettingsDB.Get().useSmallIcons;
            // CanUseBigIcons can be overriden to disable big icons permanently
            SettingsWeakEventManager.AddHandler(PageInfoViewModel.lastInstance, PageInfo_OnSettingsChanged);
        }

        private void PageInfo_OnSettingsChanged(object sender, SettingsEventArgs e)
        {
            if (e.Type == SettingsEventArgs.Setting.UseSmallIcons)
            {
                useSmallIcons = e.BoolValue;
                UpdateAllCardIcons();
            }
        }

        public bool IsCardDropAllowed(CardViewModel sourceCard, object sourceContainer)
        {
            return CanReceiveDropCards && (sourceContainer == this);
        }

        public void OnCardDragEnter(CardViewModel sourceCard, CardViewModel destCard)
        {
            sourceCard.DragImage = destCard.CardImage;
            sourceCard.CardDragMode = ECardDragMode.DragOut;

            destCard.DragImage = sourceCard.CardImage;
            destCard.CardDragMode = ECardDragMode.DragIn;
        }

        public void OnCardDragLeave(CardViewModel sourceCard, CardViewModel destCard)
        {
            sourceCard.CardDragMode = ECardDragMode.None;
            destCard.CardDragMode = ECardDragMode.None;
        }

        public void OnCardDrop(CardViewModel sourceCard, CardViewModel destCard, object sourceContainer)
        {
            sourceCard.CardDragMode = ECardDragMode.None;
            destCard.CardDragMode = ECardDragMode.None;

            int cachedSrcIdx = sourceCard.OwnerIndex;
            int cachedDestIdx = destCard.OwnerIndex;

            // OwnerIndex will be updated from Cards change notify
            Cards[cachedSrcIdx] = destCard;
            Cards[cachedDestIdx] = sourceCard;

            OnCardsChanged?.Invoke(this);
        }

        public void SetCustomSelect(RelayCommand<CardViewModel> selectCommand)
        {
            CommandSelect = selectCommand;
        }

        private void CommandSelectFunc(CardViewModel cardOb)
        {
            int idxToToggle = cardOb.OwnerIndex;
            if (selectedIndices.Contains(idxToToggle))
            {
                selectedIndices.Remove(idxToToggle);
                Cards[idxToToggle].IsHighlighted = false;
            }
            else
            {
                while (selectedIndices.Count >= NumToSelect)
                {
                    int idxToRemove = selectedIndices[0];
                    selectedIndices.RemoveAt(0);
                    Cards[idxToRemove].IsHighlighted = false;
                }

                selectedIndices.Add(idxToToggle);
                Cards[idxToToggle].IsHighlighted = true;
            }

            OnPropertyChanged("NumSelected");
            OnSelectionChanged?.Invoke(this);
        }

        private void CommandToggleLockFunc(CardViewModel cardOb)
        {
            if (cardOb != null)
            {
                cardOb.IsShowingLock = !cardOb.IsShowingLock;
            }
        }

        private void CommandPickCardFunc(CardPickerParams param)
        {
            if (param.cardVM != null && param.cardModel != null)
            {
                if (param.cardVM.CardModel == null || param.cardVM.CardModel.Id != param.cardModel.Id)
                {
                    param.cardVM.CardModel = param.cardModel;
                    OnCardsChanged?.Invoke(this);
                }
            }
        }

        private bool CardPickerItems_Filter(object item)
        {
            return !isUsingOnlyOwnedCards || ModelProxyDB.Get().OwnedCards.Contains(item as CardModelProxy);
        }

        private void Cards_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                for (int idx = 0; idx < e.NewItems.Count; idx++)
                {
                    var cardVM = e.NewItems[idx] as CardViewModel;
                    if (cardVM != null)
                    {
                        cardVM.OwnerObject = this;
                        cardVM.OwnerIndex = e.NewStartingIndex + idx;
                        UpdateCardVisuals(cardVM);
                    }
                }
            }
        }

        private void UpdateCardVisuals(CardViewModel cardVM)
        {
            cardVM.CardOwner = DeckOwner;
            cardVM.IsShowingDetails = IsShowingDetails;
            cardVM.IsUsingImageBig = CanUseBigIcons && !useSmallIcons;
            cardVM.IsPreview = SelectedPreview == cardVM;
        }

        private void UpdateAllCardIcons()
        {
            foreach (var cardVM in Cards)
            {
                if (cardVM != null)
                {
                    cardVM.IsUsingImageBig = CanUseBigIcons && !useSmallIcons;
                }
            }
        }

        public void ForceCardsUpdate()
        {
            OnCardsChanged?.Invoke(this);
        }
    }
}
