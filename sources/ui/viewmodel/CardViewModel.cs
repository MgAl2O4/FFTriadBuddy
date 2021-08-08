using System.Windows.Media.Imaging;

namespace FFTriadBuddy.UI
{
    public enum ECardOwner
    {
        None,
        Blue,
        Red,
    }

    public enum ECardDragMode
    {
        None,
        DragOut,
        DragIn,
    }

    public class CardViewModel : BaseViewModel
    {
        private CardModelProxy cardModel = null;
        public CardModelProxy CardModel
        {
            get { return cardModel; }
            set
            {
                if (cardModel == value) { return; }
                cardModel = value;

                if (cardModel != null)
                {
                    IconDB icons = IconDB.Get();
                    CardImage = isUsingImageBig ? icons.mapCardImagesBig[cardModel.cardOb.Id] : icons.mapCardImages[cardModel.cardOb.Id];
                    TypeImage = icons.mapCardTypes[cardModel.cardOb.Type];
                    RarityImage = icons.mapCardRarities[cardModel.cardOb.Rarity];
                }
                else
                {
                    CardImage = null;
                    TypeImage = null;
                    RarityImage = null;
                }

                // notify all linked properties
                OnPropertyChanged();
                OnPropertyChanged("CardImage");
                OnPropertyChanged("RarityImage");
                OnPropertyChanged("TypeImage");
                OnPropertyChanged("HasCardImage");
                OnPropertyChanged("NumUp");
                OnPropertyChanged("NumLeft");
                OnPropertyChanged("NumDown");
                OnPropertyChanged("NumRight");
                OnPropertyChanged("Tooltip");
            }
        }

        public BitmapImage CardImage { get; private set; }
        public BitmapImage RarityImage { get; private set; }
        public BitmapImage TypeImage { get; private set; }
        public bool HasCardImage => CardImage != null;

        public int NumUp => cardModel?.cardOb.Sides[(int)ETriadGameSide.Up] ?? 0;
        public int NumLeft => cardModel?.cardOb.Sides[(int)ETriadGameSide.Left] ?? 0;
        public int NumDown => cardModel?.cardOb.Sides[(int)ETriadGameSide.Down] ?? 0;
        public int NumRight => cardModel?.cardOb.Sides[(int)ETriadGameSide.Right] ?? 0;
        public string Tooltip => cardModel?.NameLocalized;

        public object OwnerObject { get; set; }

        private int ownerIndex = -1;
        public int OwnerIndex { get => ownerIndex; set => PropertySetAndNotify(value, ref ownerIndex); }

        private int numMod = 0;
        public int NumMod { get => numMod; set { PropertySetAndNotify(value, ref numMod); OnPropertyChanged("IsModNegative"); } }
        public bool IsModNegative => numMod < 0;

        private BitmapImage dragImage;
        public BitmapImage DragImage { get => dragImage; set => PropertySetAndNotify(value, ref dragImage); }

        private ECardDragMode cardDragMode;
        public ECardDragMode CardDragMode { get => cardDragMode; set => PropertySetAndNotify(value, ref cardDragMode); }

        private ECardOwner cardOwner;
        public ECardOwner CardOwner { get => cardOwner; set => PropertySetAndNotify(value, ref cardOwner); }

        private bool isHidden;
        public bool IsHidden { get => isHidden; set => PropertySetAndNotify(value, ref isHidden); }

        private bool isHighlighted;
        public bool IsHighlighted { get => isHighlighted; set => PropertySetAndNotify(value, ref isHighlighted); }

        private bool isPreview;
        public bool IsPreview { get => isPreview; set => PropertySetAndNotify(value, ref isPreview); }

        private bool isShowingDetails = true;
        public bool IsShowingDetails { get => isShowingDetails; set => PropertySetAndNotify(value, ref isShowingDetails); }

        private bool isShowingLock = false;
        public bool IsShowingLock { get => isShowingLock; set => PropertySetAndNotify(value, ref isShowingLock); }

        private bool isUsingImageBig = false;
        public bool IsUsingImageBig
        {
            get => isUsingImageBig;
            set
            {
                PropertySetAndNotify(value, ref isUsingImageBig);

                IconDB icons = IconDB.Get();
                CardImage =
                    (cardModel == null) ? null :
                    isUsingImageBig ? icons.mapCardImagesBig[cardModel.cardOb.Id] :
                    icons.mapCardImages[CardModel.cardOb.Id];
                OnPropertyChanged();
                OnPropertyChanged("CardImage");
                OnPropertyChanged("HasCardImage");
            }
        }

        public void Assign(TriadCardInstance cardData)
        {
            CardModel = (cardData == null) ? null : ModelProxyDB.Get().GetCardProxy(cardData.card);
            NumMod = cardData?.scoreModifier ?? 0;
            IsShowingDetails = cardData != null;
            CardOwner = (cardData == null) ? ECardOwner.None :
                (cardData.owner == ETriadCardOwner.Blue) ? ECardOwner.Blue
                : ECardOwner.Red;
        }
    }
}
