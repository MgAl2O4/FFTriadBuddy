using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace FFTriadBuddy.UI
{
    public class AdjustCardViewModel : LocalizedViewModel, IDialogWindowViewModel
    {
        public ImageCardDataViewModel HashProxy { get; private set; }

        private int sideUp = 0;
        public int SideUp { get => sideUp; set { PropertySetAndNotify(value, ref sideUp); UpdateMatchingCards(); } }

        private int sideLeft = 0;
        public int SideLeft { get => sideLeft; set { PropertySetAndNotify(value, ref sideLeft); UpdateMatchingCards(); } }

        private int sideDown = 0;
        public int SideDown { get => sideDown; set { PropertySetAndNotify(value, ref sideDown); UpdateMatchingCards(); } }

        private int sideRight = 0;
        public int SideRight { get => sideRight; set { PropertySetAndNotify(value, ref sideRight); UpdateMatchingCards(); } }

        public string DescSideUp { get; private set; }
        public string DescSideLeft { get; private set; }
        public string DescSideDown { get; private set; }
        public string DescSideRight { get; private set; }

        public BitmapImage ImageSideUp { get; } = new BitmapImage();
        public BitmapImage ImageSideLeft { get; } = new BitmapImage();
        public BitmapImage ImageSideDown { get; } = new BitmapImage();
        public BitmapImage ImageSideRight { get; } = new BitmapImage();

        public BitmapImage ImageCard { get; } = new BitmapImage();
        public string CardDesc => (HashProxy == null) ? null : string.Format("{0} [{1}-{2}-{3}-{4}]",
            HashProxy.cardState.card == null ? loc.strings.AdjustForm_Dynamic_UnknownOwner : HashProxy.cardState.card.Name.GetLocalized(),
            HashProxy.cardState.sideNumber[0], HashProxy.cardState.sideNumber[1], HashProxy.cardState.sideNumber[2], HashProxy.cardState.sideNumber[3]);

        public string CardState => (HashProxy == null) ? null : GetLocalizedCardState(HashProxy.cardState.state);

        public BulkObservableCollection<CardModelProxy> MatchingCards { get; } = new BulkObservableCollection<CardModelProxy>();
        private CardModelProxy selectedMatch;
        public CardModelProxy SelectedMatch { get => selectedMatch; set => PropertySetAndNotify(value, ref selectedMatch); }
        public bool HasMultipleMatches => MatchingCards.Count > 1;

        public ICommand CommandSave { get; private set; }

        public event Action<bool?> RequestDialogWindowClose;

        public string AdjustForm_CancelButton => loc.strings.AdjustForm_CancelButton;
        public string AdjustForm_SaveButton => loc.strings.AdjustForm_SaveButton;
        public string AdjustForm_CardDown => loc.strings.AdjustForm_CardDown;
        public string AdjustForm_CardLeft => loc.strings.AdjustForm_CardLeft;
        public string AdjustForm_CardList => loc.strings.AdjustForm_CardList;
        public string AdjustForm_CardRight => loc.strings.AdjustForm_CardRight;
        public string AdjustForm_CardStatus => loc.strings.AdjustForm_CardStatus;
        public string AdjustForm_CardUp => loc.strings.AdjustForm_CardUp;
        public string AdjustForm_Current => loc.strings.AdjustForm_Current;

        public AdjustCardViewModel()
        {
            // design time only
        }

        public AdjustCardViewModel(ImageCardDataViewModel cardData)
        {
            HashProxy = cardData;

            SideUp = HashProxy.cardState.sideNumber[0];
            SideLeft = HashProxy.cardState.sideNumber[1];
            SideDown = HashProxy.cardState.sideNumber[2];
            SideRight = HashProxy.cardState.sideNumber[3];

            DescSideUp = GenerateSideInfo(ImageSideUp, HashProxy.cardState.sideInfo[0]);
            DescSideLeft = GenerateSideInfo(ImageSideLeft, HashProxy.cardState.sideInfo[1]);
            DescSideDown = GenerateSideInfo(ImageSideDown, HashProxy.cardState.sideInfo[2]);
            DescSideRight = GenerateSideInfo(ImageSideRight, HashProxy.cardState.sideInfo[3]);
            GenerateCardImage();
            UpdateMatchingCards();

            CommandSave = new RelayCommand<object>((_) => RequestDialogWindowClose.Invoke(true), (_) => (HashProxy != null) && (selectedMatch != null) && (selectedMatch.cardOb != HashProxy.cardState.card));
        }

        public string GetDialogWindowTitle()
        {
            return loc.strings.AdjustForm_Title;
        }

        private string GetLocalizedCardState(ScannerTriad.ECardState value)
        {
            switch (value)
            {
                case ScannerTriad.ECardState.Hidden:
                    return loc.strings.AdjustForm_Dynamic_CardState_Hidden;
                case ScannerTriad.ECardState.Locked:
                    return loc.strings.AdjustForm_Dynamic_CardState_Locked;
                case ScannerTriad.ECardState.Visible:
                    return loc.strings.AdjustForm_Dynamic_CardState_Visible;
                case ScannerTriad.ECardState.PlacedRed:
                    return loc.strings.AdjustForm_Dynamic_CardState_PlacedRed;
                case ScannerTriad.ECardState.PlacedBlue:
                    return loc.strings.AdjustForm_Dynamic_CardState_PlacedBlue;
            }
            return loc.strings.AdjustForm_Dynamic_CardState_None;
        }

        private string GenerateSideInfo(BitmapImage image, ScannerTriad.CardState.SideInfo sideInfo)
        {
            var hashSize = ScannerTriad.GetDigitHashSize();
            var hashPreview = new ImageUtils.HashPreview
            {
                bounds = new System.Drawing.Rectangle(0, 0, hashSize.Width, hashSize.Height),
                hashValues = sideInfo.hashValues
            };

            var bitmap = new System.Drawing.Bitmap(hashSize.Width, hashSize.Height, PixelFormat.Format32bppArgb);
            ImageUtils.DrawDebugHash(bitmap, hashPreview, System.Drawing.Color.White);

            WriteBitmapToImage(bitmap, image);
            bitmap.Dispose();

            return sideInfo.hasOverride ? loc.strings.AdjustForm_Dynamic_Digit_Override :
                string.Format("{0}: {1:P0}", loc.strings.AdjustForm_Dynamic_Digit_Default, sideInfo.matchPct);
        }

        private void GenerateCardImage()
        {
            var bitmap = ImageUtils.CreatePreviewImage(HashProxy.cardState.sourceImage, HashProxy.cardState.bounds, System.Drawing.Rectangle.Empty);
            WriteBitmapToImage(bitmap, ImageCard);
            bitmap.Dispose();
        }

        private void WriteBitmapToImage(System.Drawing.Bitmap bitmap, BitmapImage image)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                image.BeginInit();
                image.StreamSource = memory;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
            }
        }

        private void UpdateMatchingCards()
        {
            MatchingCards.SuspendNotifies();
            MatchingCards.Clear();

            var cardDB = TriadCardDB.Get();
            TriadCard foundCard = cardDB.Find(SideUp, SideLeft, SideDown, SideRight);

            CardModelProxy newMatch = null;
            if (foundCard != null)
            {
                var modelProxyDB = ModelProxyDB.Get();

                if (foundCard.SameNumberId < 0)
                {
                    MatchingCards.Add(modelProxyDB.GetCardProxy(foundCard));
                }
                else
                {
                    foreach (var card in cardDB.sameNumberMap[foundCard.SameNumberId])
                    {
                        MatchingCards.Add(modelProxyDB.GetCardProxy(foundCard));
                    }
                }

                newMatch = MatchingCards[0];
            }

            if (selectedMatch != newMatch)
            {
                SelectedMatch = newMatch;
                CommandManager.InvalidateRequerySuggested();
            }

            MatchingCards.ResumeNotifies();
        }
    }
}
