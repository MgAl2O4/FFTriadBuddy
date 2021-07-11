using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace FFTriadBuddy.UI
{
    public interface IImageHashMatch
    {
        string NameLocalized { get; }
        object GetMatchOwner();
    }

    public class ImageHashDataModelProxy : LocalizedViewModel
    {
        public class NumberVM : IComparable, IImageHashMatch
        {
            public int Value;
            public string NameLocalized => Value.ToString("X");

            public int CompareTo(object obj)
            {
                return Value.CompareTo((obj as NumberVM).Value);
            }

            public object GetMatchOwner()
            {
                return Value;
            }
        }

        public readonly ImageHashData hashData;

        public string DescMatch =>
            hashData.isAuto ? loc.strings.MainForm_Dynamic_Screenshot_MatchType_Auto :
            (hashData.matchDistance == 0) ? loc.strings.MainForm_Dynamic_Screenshot_MatchType_Exact :
            loc.strings.MainForm_Dynamic_Screenshot_MatchType_Similar;

        private string cachedName;
        public string NameLocalized => cachedName;

        private string cachedType;
        public string TypeLocalized => cachedType;

        private BitmapImage cachedPreview;
        public BitmapImage PreviewImage { get { GeneratePreview(); return cachedPreview; } }

        private List<IImageHashMatch> listMatches = new List<IImageHashMatch>();
        public List<IImageHashMatch> ListMatches { get { GenerateMatches(); return listMatches; } }

        private IImageHashMatch currentMatch = null;
        public IImageHashMatch CurrentMatch { get { GenerateMatches(); return currentMatch; } }

        private static List<NumberVM> listCactpotNumbers;
        private static List<NumberVM> listCardNumbers;

        public ImageHashDataModelProxy(ImageHashData hashData)
        {
            this.hashData = hashData;
            UpdateCachedText();
        }

        public override void RefreshLocalization()
        {
            UpdateCachedText();
            OnPropertyChanged("DescMatch");
            OnPropertyChanged("NameLocalized");
            OnPropertyChanged("TypeLocalized");
        }

        public void UpdateCachedText()
        {
            switch (hashData.type)
            {
                case EImageHashType.Rule: cachedType = loc.strings.MainForm_Dynamic_Screenshot_HashType_Rule; break;
                case EImageHashType.Cactpot: cachedType = loc.strings.MainForm_Dynamic_Screenshot_HashType_Number; break;
                case EImageHashType.CardImage: cachedType = loc.strings.MainForm_Dynamic_Screenshot_HashType_Card; break;
                case EImageHashType.CardNumber: cachedType = loc.strings.MainForm_Dynamic_Screenshot_HashType_Number; break;
                default: cachedType = "??"; break;
            }

            string descName = "??";
            if (hashData.ownerOb != null)
            {
                switch (hashData.type)
                {
                    case EImageHashType.Rule: descName = (hashData.ownerOb as TriadGameModifier).GetLocalizedName(); break;
                    case EImageHashType.Cactpot: descName = ((int)hashData.ownerOb).ToString(); break;
                    case EImageHashType.CardImage: descName = (hashData.ownerOb as TriadCard).ToShortLocalizedString(); break;
                    case EImageHashType.CardNumber: descName = ((int)hashData.ownerOb).ToString(); break;
                    default: break;
                }
            }

            cachedName = cachedType + ": " + descName;
        }

        private void GeneratePreview()
        {
            if (cachedPreview == null)
            {
                hashData.UpdatePreviewImage();

                using (var memory = new MemoryStream())
                {
                    hashData.previewImage.Save(memory, ImageFormat.Png);
                    memory.Position = 0;

                    cachedPreview = new BitmapImage();
                    cachedPreview.BeginInit();
                    cachedPreview.StreamSource = memory;
                    cachedPreview.CacheOption = BitmapCacheOption.OnLoad;
                    cachedPreview.EndInit();
                    cachedPreview.Freeze();
                }
            }
        }

        private void GenerateMatches()
        {
            if (listMatches.Count > 0)
            {
                return;
            }

            var modelProxyDB = ModelProxyDB.Get();
            switch (hashData.type)
            {
                case EImageHashType.Rule:
                    foreach (var rule in modelProxyDB.Rules)
                    {
                        if ((rule.modOb is TriadGameModifierNone) == false)
                        {
                            listMatches.Add(rule);
                        }
                    }
                    listMatches.Sort();
                    break;

                case EImageHashType.Cactpot:
                    if (listCactpotNumbers == null)
                    {
                        listCactpotNumbers = new List<NumberVM>();
                        for (int idx = 1; idx <= 9; idx++)
                        {
                            listCactpotNumbers.Add(new NumberVM() { Value = idx });
                        }
                    }

                    listMatches.AddRange(listCactpotNumbers);
                    break;

                case EImageHashType.CardImage:
                    var sameNumberId = ((TriadCard)hashData.ownerOb).SameNumberId;
                    foreach (var cardOb in TriadCardDB.Get().sameNumberMap[sameNumberId])
                    {
                        listMatches.Add(modelProxyDB.GetCardProxy(cardOb));
                    }
                    listMatches.Sort();
                    break;

                case EImageHashType.CardNumber:
                    if (listCardNumbers == null)
                    {
                        listCardNumbers = new List<NumberVM>();
                        for (int idx = 1; idx <= 10; idx++)
                        {
                            listCardNumbers.Add(new NumberVM() { Value = idx });
                        }
                    }

                    listMatches.AddRange(listCardNumbers);
                    break;

                default:
                    break;
            }

            currentMatch = listMatches.Find(x => x.GetMatchOwner().Equals(hashData.ownerOb));
        }
    }
}
