using System;

namespace FFTriadBuddy.UI
{
    // viewmodel wrapper for model class: card
    public class CardModelProxy : BaseViewModel, IComparable, IImageHashMatch
    {
        public readonly TriadCard cardOb;

        public string NameLocalized => cardOb.Name.GetLocalized();
        public string DescDeckPicker => string.Format("{0} ({1})", NameLocalized, new string('*', (int)cardOb.Rarity + 1));
        public int GameSortGroup => cardOb.SortOrder / 1000;
        public int GameSortOrder => cardOb.SortOrder;
        public int Id => cardOb.Id;

        public string DescPower => string.Format("{0:X}-{1:X}-{2:X}-{3:X}", cardOb.Sides[(int)ETriadGameSide.Up], cardOb.Sides[(int)ETriadGameSide.Left], cardOb.Sides[(int)ETriadGameSide.Down], cardOb.Sides[(int)ETriadGameSide.Right]);
        public string DescRarity { get; private set; }
        public ETriadCardRarity Rarity => cardOb.Rarity;
        public string DescCardType => LocalizationDB.Get().mapCardTypes[cardOb.Type].GetLocalized();
        public ETriadCardType CardType => cardOb.Type;

        private bool isOwned;
        public bool IsOwned
        {
            get => isOwned;
            set
            {
                if (isOwned != value)
                {
                    isOwned = value;
                    OnPropertyChanged();
                    ModelProxyDB.Get().UpdateOwnedCard(this);
                }
            }
        }

        public int CompareTo(object obj)
        {
            var otherCard = obj as CardModelProxy;
            return (otherCard != null) ? NameLocalized.CompareTo(otherCard.NameLocalized) : 0;
        }

        public CardModelProxy(TriadCard triadCard)
        {
            cardOb = triadCard;

            DescRarity = "*";
            for (int idx = 0; idx < (int)triadCard.Rarity; idx++)
            {
                DescRarity += " *";
            }
        }

        public object GetMatchOwner()
        {
            return cardOb;
        }
    }
}
