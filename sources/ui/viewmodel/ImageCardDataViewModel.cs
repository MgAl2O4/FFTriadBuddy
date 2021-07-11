namespace FFTriadBuddy.UI
{
    public class ImageCardDataViewModel : LocalizedViewModel
    {
        public readonly ScannerTriad.CardState cardState;

        private string cachedType;
        public string DescType => cachedType;

        private string cachedSides;
        public string DescSides => cachedSides;

        private string cachedName;
        public string NameLocalized => cachedName;

        public bool IsDetected => cardState.card != null;

        public ImageCardDataViewModel(ScannerTriad.CardState cardState)
        {
            this.cardState = cardState;
            cachedSides =
                (cardState.sideNumber == null) ? "" :
                string.Format("{0}-{1}-{2}-{3}", cardState.sideNumber[0], cardState.sideNumber[1], cardState.sideNumber[2], cardState.sideNumber[3]);

            UpdateCachedText();
        }

        public override void RefreshLocalization()
        {
            UpdateCachedText();
            OnPropertyChanged("DescType");
            OnPropertyChanged("NameLocalized");
            // DescSides doesn't depend on loc
        }

        public void UpdateCachedText()
        {
            cachedType =
                (cardState.location == ScannerTriad.ECardLocation.BlueDeck) ? string.Format(loc.strings.MainForm_Dynamic_Screenshot_CardLocation_BlueDeck, cardState.locationContext + 1) :
                (cardState.location == ScannerTriad.ECardLocation.RedDeck) ? string.Format(loc.strings.MainForm_Dynamic_Screenshot_CardLocation_RedDeck, cardState.locationContext + 1) :
                string.Format(loc.strings.MainForm_Dynamic_Screenshot_CardLocation_Board,
                    cardState.locationContext < 3 ? loc.strings.MainForm_Dynamic_Screenshot_BoardRow_Top :
                        (cardState.locationContext < 6) ? loc.strings.MainForm_Dynamic_Screenshot_BoardRow_Middle :
                        loc.strings.MainForm_Dynamic_Screenshot_BoardRow_Bottom,
                    (cardState.locationContext % 3) + 1);

            cachedName = cardState.card == null ? loc.strings.MainForm_Dynamic_Screenshot_CardNotDetected : cardState.card.Name.GetLocalized();
        }
    }
}
