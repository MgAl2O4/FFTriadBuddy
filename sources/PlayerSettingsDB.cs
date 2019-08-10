using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace FFTriadBuddy
{
    public class PlayerSettingsDB
    {
        public List<TriadCard> ownedCards;
        public List<TriadNpc> completedNpcs;
        public List<ImageHashData> customHashes;
        public List<ImagePatternDigit> customDigits;
        public TriadCard[] starterCards;
        public Dictionary<TriadNpc, TriadDeck> lastDeck;
        public string DBPath;
        private List<ImageHashData> lockedHashes;
        private static PlayerSettingsDB instance = new PlayerSettingsDB();

        public PlayerSettingsDB()
        {
            DBPath = "player.xml";
            ownedCards = new List<TriadCard>();
            completedNpcs = new List<TriadNpc>();
            lastDeck = new Dictionary<TriadNpc, TriadDeck>();
            starterCards = new TriadCard[5];
            customHashes = new List<ImageHashData>();
            customDigits = new List<ImagePatternDigit>();
            lockedHashes = new List<ImageHashData>();
        }

        public static PlayerSettingsDB Get()
        {
            return instance;
        }

        public bool Load()
        {
            string FilePath = AssetManager.Get().CreateFilePath(DBPath);
            TriadCardDB cardDB = TriadCardDB.Get();
            TriadNpcDB npcDB = TriadNpcDB.Get();

            if (File.Exists(FilePath))
            {
                try
                {
                    XmlDocument xdoc = new XmlDocument();
                    xdoc.Load(FilePath);

                    foreach (XmlNode testNode in xdoc.DocumentElement.ChildNodes)
                    {
                        try
                        {
                            XmlElement testElem = (XmlElement)testNode;
                            if (testElem != null && testElem.Name == "card")
                            {
                                int cardId = int.Parse(testElem.GetAttribute("id"));
                                ownedCards.Add(cardDB.cards[cardId]);
                            }
                            else if (testElem != null && testElem.Name == "npc")
                            {
                                int npcId = int.Parse(testElem.GetAttribute("id"));
                                completedNpcs.Add(npcDB.npcs[npcId]);
                            }
                            else if (testElem != null && testElem.Name == "deck")
                            {
                                int npcId = int.Parse(testElem.GetAttribute("id"));
                                TriadNpc npc = TriadNpcDB.Get().npcs[npcId];
                                if (npc != null)
                                {
                                    TriadDeck deckCards = new TriadDeck();
                                    foreach (XmlAttribute attr in testElem.Attributes)
                                    {
                                        if (attr.Name.StartsWith("card"))
                                        {
                                            string cardNumStr = attr.Name.Substring(4);
                                            int cardNum = int.Parse(cardNumStr);
                                            while (deckCards.knownCards.Count < (cardNum + 1))
                                            {
                                                deckCards.knownCards.Add(null);
                                            }

                                            int cardId = int.Parse(attr.Value);
                                            deckCards.knownCards[cardNum] = TriadCardDB.Get().cards[cardId];
                                        }
                                    }

                                    lastDeck.Add(npc, deckCards);
                                }
                            }
                            else
                            {
                                ImageHashData customHash = ImageHashDB.Get().LoadHashEntry(testElem);
                                if (customHash != null)
                                {
                                    customHashes.Add(customHash);
                                }
                                else
                                {
                                    ImagePatternDigit customDigit = ImageHashDB.Get().LoadDigitEntry(testElem);
                                    if (customDigit.Value > 0)
                                    {
                                        customDigits.Add(customDigit);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine("Loading failed! Exception:" + ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Loading failed! Exception:" + ex);
                }
            }

            starterCards[0] = cardDB.Find("Dodo");
            starterCards[1] = cardDB.Find("Sabotender");
            starterCards[2] = cardDB.Find("Bomb");
            starterCards[3] = cardDB.Find("Mandragora");
            starterCards[4] = cardDB.Find("Coeurl");
            foreach (TriadCard starterCard in starterCards)
            {
                if (!ownedCards.Contains(starterCard))
                {
                    ownedCards.Add(starterCard);
                }
            }

            Logger.WriteLine("Loaded player cards: " + ownedCards.Count + ", npcs: " + completedNpcs.Count + ", hashes: " + customHashes.Count);
            return ownedCards.Count > 0;
        }

        public void Save()
        {
            string FilePath = AssetManager.Get().CreateFilePath(DBPath);
            try
            {
                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.Indent = true;

                XmlWriter xmlWriter = XmlWriter.Create(FilePath, writerSettings);
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("root");

                List<int> ownedIds = new List<int>();
                foreach (TriadCard card in ownedCards)
                {
                    ownedIds.Add(card.Id);
                }

                ownedIds.Sort();
                foreach (int id in ownedIds)
                {
                    xmlWriter.WriteStartElement("card");
                    xmlWriter.WriteAttributeString("id", id.ToString());
                    xmlWriter.WriteEndElement();
                }

                ownedIds.Clear();
                foreach (TriadNpc npc in completedNpcs)
                {
                    ownedIds.Add(npc.Id);
                }

                ownedIds.Sort();
                foreach (int id in ownedIds)
                {
                    xmlWriter.WriteStartElement("npc");
                    xmlWriter.WriteAttributeString("id", id.ToString());
                    xmlWriter.WriteEndElement();
                }

                foreach (KeyValuePair<TriadNpc, TriadDeck> kvp in lastDeck)
                {
                    xmlWriter.WriteStartElement("deck");
                    xmlWriter.WriteAttributeString("id", kvp.Key.Id.ToString());
                    for (int Idx = 0; Idx < kvp.Value.knownCards.Count; Idx++)
                    {
                        xmlWriter.WriteAttributeString("card" + Idx, kvp.Value.knownCards[Idx].Id.ToString());
                    }

                    xmlWriter.WriteEndElement();
                }

                customHashes.Sort();
                foreach (ImageHashData customHash in customHashes)
                {
                    ImageHashDB.Get().StoreEntry(customHash, xmlWriter);
                }

                customDigits.Sort();
                foreach (ImagePatternDigit customDigit in customDigits)
                {
                    ImageHashDB.Get().StoreEntry(customDigit, xmlWriter);
                }

                xmlWriter.WriteEndDocument();
                xmlWriter.Close();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Saving failed! Exception:" + ex);
            }
        }

        public void AddLockedHash(ImageHashData hashData)
        {
            if (!IsLockedHash(hashData))
            {
                for (int Idx = 0; Idx < customHashes.Count; Idx++)
                {
                    int testDistance = customHashes[Idx].GetDistance(hashData);
                    if (testDistance == 0)
                    {
                        customHashes.RemoveAt(Idx);
                        break;
                    }
                }

                lockedHashes.Add(hashData);
            }
        }

        public bool IsLockedHash(Palit.TLSHSharp.TlshHash hash)
        {
            foreach (ImageHashData testData in lockedHashes)
            {
                int testDistance = testData.GetDistance(hash);
                if (testDistance == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsLockedHash(ImageHashData hashData)
        {
            return IsLockedHash(hashData.Hash);
        }

        public void AddKnownHash(ImageHashData hashData)
        {
            for (int Idx = 0; Idx < lockedHashes.Count; Idx++)
            {
                int testDistance = lockedHashes[Idx].GetDistance(hashData);
                if (testDistance == 0)
                {
                    lockedHashes.RemoveAt(Idx);
                    break;
                }
            }

            customHashes.Add(hashData);
        }

        public void AddKnownDigit(ImagePatternDigit digitData)
        {
            for (int Idx = 0; Idx < customDigits.Count; Idx++)
            {
                if (customDigits[Idx].Hash == digitData.Hash)
                {
                    customDigits[Idx] = digitData;
                    return;
                }
            }

            customDigits.Add(digitData);
        }
    }
}
