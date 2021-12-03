using MgAl2O4.Utils;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace FFTriadBuddy.UI
{
    public class IconDB
    {
        public List<BitmapImage> mapCardImages;
        public List<BitmapImage> mapCardImagesBig;
        public Dictionary<ETriadCardType, BitmapImage> mapCardTypes;
        public Dictionary<ETriadCardRarity, BitmapImage> mapCardRarities;
        public Dictionary<string, BitmapImage> mapFlags;

        private static IconDB instance = new IconDB();
        public static IconDB Get() { return instance; }

        public void Load()
        {
            LoadCardImages();
            LoadCardTypes();
            LoadCardRarities();
            LoadFlags();
        }

        private BitmapImage LoadImageFromAsset(string path)
        {
            var image = new BitmapImage();
            using (var fileStream = AssetManager.Get().GetAsset(path))
            {
                using (var memStream = new MemoryStream())
                {
                    fileStream.CopyTo(memStream);
                    memStream.Position = 0;

                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = memStream;
                    image.EndInit();
                    image.Freeze();
                }
            }

            return image;
        }

        private void LoadCardImages()
        {
            mapCardImages = new List<BitmapImage>();
            mapCardImagesBig = new List<BitmapImage>();

            string nullImagePath = "icons/082500.png";
            var nullImg = LoadImageFromAsset(nullImagePath);

            string nullImageBigPath = "icons/082100.png";
            var nullImgBig = LoadImageFromAsset(nullImageBigPath);

            TriadCardDB cardDB = TriadCardDB.Get();
            for (int idx = 0; idx < cardDB.cards.Count; idx++)
            {
                var cardOb = cardDB.cards[idx];
                if (cardOb != null && cardOb.IsValid())
                {
                    string loadPath = "icons/" + cardOb.IconPath;
                    var loadedImage = LoadImageFromAsset(loadPath);
                    mapCardImages.Add(loadedImage);

                    var loadPathIdStr = loadPath.Substring(loadPath.Length - 7, 3);
                    int loadPathId = int.Parse(loadPathIdStr);

                    string loadPathBig = loadPath.Substring(0, loadPath.Length - 7);
                    loadPathBig += (loadPathId - 400);
                    loadPathBig += ".png";
                    loadedImage = LoadImageFromAsset(loadPathBig);
                    mapCardImagesBig.Add(loadedImage);
                }
                else
                {
                    mapCardImages.Add(nullImg);
                    mapCardImagesBig.Add(nullImgBig);
                }
            }
        }

        private void LoadCardTypes()
        {
            mapCardTypes = new Dictionary<ETriadCardType, BitmapImage>();
            mapCardTypes.Add(ETriadCardType.None, null);
            mapCardTypes.Add(ETriadCardType.Beastman, LoadImageFromAsset("parts/typeBeastman.png"));
            mapCardTypes.Add(ETriadCardType.Primal, LoadImageFromAsset("parts/typePrimal.png"));
            mapCardTypes.Add(ETriadCardType.Scion, LoadImageFromAsset("parts/typeScions.png"));
            mapCardTypes.Add(ETriadCardType.Garlean, LoadImageFromAsset("parts/typeGarland.png"));
        }

        private void LoadCardRarities()
        {
            mapCardRarities = new Dictionary<ETriadCardRarity, BitmapImage>();
            mapCardRarities.Add(ETriadCardRarity.Common, LoadImageFromAsset("parts/rarityCommon.png"));
            mapCardRarities.Add(ETriadCardRarity.Uncommon, LoadImageFromAsset("parts/rarityUncommon.png"));
            mapCardRarities.Add(ETriadCardRarity.Rare, LoadImageFromAsset("parts/rarityRare.png"));
            mapCardRarities.Add(ETriadCardRarity.Epic, LoadImageFromAsset("parts/rarityEpic.png"));
            mapCardRarities.Add(ETriadCardRarity.Legendary, LoadImageFromAsset("parts/rarityLegendary.png"));
        }

        private void LoadFlags()
        {
            mapFlags = new Dictionary<string, BitmapImage>();
            mapFlags.Add("de", LoadImageFromAsset("flags/flag-germany.png"));
            mapFlags.Add("en", LoadImageFromAsset("flags/flag-usa.png"));
            mapFlags.Add("es", LoadImageFromAsset("flags/flag-spain.png"));
            mapFlags.Add("fr", LoadImageFromAsset("flags/flag-france.png"));
            mapFlags.Add("ja", LoadImageFromAsset("flags/flag-japan.png"));
            mapFlags.Add("zh", LoadImageFromAsset("flags/flag-china.png"));
            mapFlags.Add("ko", LoadImageFromAsset("flags/flag-southkorea.png"));
        }
    }
}
