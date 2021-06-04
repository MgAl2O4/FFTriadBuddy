using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace FFTriadBuddy
{
    public class TriadCardDB
    {
        public List<TriadCard> cards;
        public TriadCard hiddenCard;
        public string DBPath;
        public Dictionary<int, List<TriadCard>> sameNumberMap;
        private static TriadCardDB instance = new TriadCardDB();

        public TriadCardDB()
        {
            DBPath = "data/cards.xml";
            cards = new List<TriadCard>();
            hiddenCard = new TriadCard(0, null, ETriadCardRarity.Common, ETriadCardType.None, 0, 0, 0, 0, 0, 0);
            hiddenCard.Name.Text[LocalizationDB.CodeLanguageIdx] = "(hidden)";

            sameNumberMap = new Dictionary<int, List<TriadCard>>();
        }

        public static TriadCardDB Get()
        {
            return instance;
        }

        public bool Load()
        {
            try
            {
                XmlDocument xdoc = new XmlDocument();
                Stream dataStream = AssetManager.Get().GetAsset(DBPath);
                xdoc.Load(dataStream);

                foreach (XmlNode cardNode in xdoc.DocumentElement.ChildNodes)
                {
                    XmlElement cardElem = (XmlElement)cardNode;
                    if (cardElem != null && cardElem.Name == "card")
                    {
                        try
                        {
                            ETriadCardRarity cardRarity = (ETriadCardRarity)int.Parse(cardElem.GetAttribute("rarity"));
                            ETriadCardType cardType = (ETriadCardType)int.Parse(cardElem.GetAttribute("type"));
                            int sortOrder = int.Parse(cardElem.GetAttribute("sort"));
                            int cardGroup = int.Parse(cardElem.GetAttribute("group"));

                            TriadCard newCard = new TriadCard(
                                int.Parse(cardElem.GetAttribute("id")),
                                cardElem.GetAttribute("icon"),
                                cardRarity,
                                cardType,
                                ParseCardSideNum(cardElem.GetAttribute("up")),
                                ParseCardSideNum(cardElem.GetAttribute("dn")),
                                ParseCardSideNum(cardElem.GetAttribute("lt")),
                                ParseCardSideNum(cardElem.GetAttribute("rt")),
                                sortOrder,
                                cardGroup);

                            while (cards.Count <= newCard.Id)
                            {
                                cards.Add(null);
                            }

                            cards[newCard.Id] = newCard;
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine("Loading failed! Exception:" + ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Loading failed! Exception:" + ex);
            }

            sameNumberMap.Clear();
            int sameNumberId = 0;
            for (int Idx1 = 0; Idx1 < cards.Count; Idx1++)
            {
                TriadCard card1 = cards[Idx1];
                if (card1 != null && card1.SameNumberId < 0)
                {
                    bool bHasSameNumberCards = false;
                    for (int Idx2 = (Idx1 + 1); Idx2 < cards.Count; Idx2++)
                    {
                        TriadCard card2 = cards[Idx2];
                        if (card2 != null && card2.SameNumberId < 0)
                        {
                            bool bHasSameNumbers =
                                (card1.Sides[0] == card2.Sides[0]) &&
                                (card1.Sides[1] == card2.Sides[1]) &&
                                (card1.Sides[2] == card2.Sides[2]) &&
                                (card1.Sides[3] == card2.Sides[3]);

                            bHasSameNumberCards = bHasSameNumberCards || bHasSameNumbers;
                            if (bHasSameNumbers)
                            {
                                if (!sameNumberMap.ContainsKey(sameNumberId))
                                {
                                    sameNumberMap.Add(sameNumberId, new List<TriadCard>());
                                    sameNumberMap[sameNumberId].Add(card1);
                                    card1.SameNumberId = sameNumberId;
                                }

                                sameNumberMap[sameNumberId].Add(card2);
                                card2.SameNumberId = sameNumberId;
                            }
                        }
                    }

                    if (bHasSameNumberCards)
                    {
                        sameNumberId++;
                    }
                }
            }

            Logger.WriteLine("Loaded cards: " + cards.Count + ", same sides: " + sameNumberMap.Count);
            return cards.Count > 0;
        }

        public void Save()
        {
            string RawFilePath = AssetManager.Get().CreateFilePath("assets/" + DBPath);
            try
            {
                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.Indent = true;

                XmlWriter xmlWriter = XmlWriter.Create(RawFilePath, writerSettings);
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("root");

                foreach (TriadCard card in cards)
                {
                    if (card != null)
                    {
                        xmlWriter.WriteStartElement("card");
                        xmlWriter.WriteAttributeString("id", card.Id.ToString());
                        xmlWriter.WriteAttributeString("icon", card.IconPath);
                        xmlWriter.WriteAttributeString("rarity", ((int)card.Rarity).ToString());
                        xmlWriter.WriteAttributeString("type", ((int)card.Type).ToString());
                        xmlWriter.WriteAttributeString("up", card.Sides[(int)ETriadGameSide.Up].ToString());
                        xmlWriter.WriteAttributeString("lt", card.Sides[(int)ETriadGameSide.Left].ToString());
                        xmlWriter.WriteAttributeString("dn", card.Sides[(int)ETriadGameSide.Down].ToString());
                        xmlWriter.WriteAttributeString("rt", card.Sides[(int)ETriadGameSide.Right].ToString());
                        xmlWriter.WriteAttributeString("sort", card.SortOrder.ToString());
                        xmlWriter.WriteAttributeString("group", card.Group.ToString());
                        xmlWriter.WriteEndElement();
                    }
                }

                xmlWriter.WriteEndDocument();
                xmlWriter.Close();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Saving failed! Exception:" + ex);
            }
        }

        public TriadCard Find(string Name)
        {
            foreach (TriadCard testCard in cards)
            {
                if (testCard != null &&
                    testCard.Name.GetCodeName().Equals(Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return testCard;
                }
            }

            return null;
        }

        public TriadCard Find(int numUp, int numLeft, int numDown, int numRight)
        {
            foreach (TriadCard testCard in cards)
            {
                if (testCard != null &&
                    testCard.Sides[(int)ETriadGameSide.Up] == numUp &&
                    testCard.Sides[(int)ETriadGameSide.Down] == numDown &&
                    testCard.Sides[(int)ETriadGameSide.Left] == numLeft &&
                    testCard.Sides[(int)ETriadGameSide.Right] == numRight)
                {
                    return testCard;
                }
            }

            return null;
        }

        private int ParseCardSideNum(string desc)
        {
            if (desc == "A" || desc == "a" || desc == "10")
            {
                return 10;
            }

            if (desc.Length == 1 && desc[0] >= '1' && desc[0] <= '9')
            {
                return desc[0] - '0';
            }

            return -1;
        }
    }
}
