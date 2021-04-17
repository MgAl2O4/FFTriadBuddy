using MgAl2O4.Utils;
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
        public List<TriadDeckNamed> favDecks;
        public bool useAutoScan;
        public bool useFullScreenCapture;
        public bool useCloudStorage;
        public bool useXInput;
        public bool isDirty;
        public string DBPath;
        public string cloudToken;
        private List<ImageHashData> lockedHashes;
        private static PlayerSettingsDB instance = new PlayerSettingsDB();

        public delegate void UpdatedDelegate(bool bCards, bool bNpcs, bool bDecks);
        public event UpdatedDelegate OnUpdated;

        public PlayerSettingsDB()
        {
            DBPath = "FFTriadBuddy-settings.json";
            ownedCards = new List<TriadCard>();
            completedNpcs = new List<TriadNpc>();
            lastDeck = new Dictionary<TriadNpc, TriadDeck>();
            favDecks = new List<TriadDeckNamed>();
            starterCards = new TriadCard[5];
            customHashes = new List<ImageHashData>();
            customDigits = new List<ImagePatternDigit>();
            lockedHashes = new List<ImageHashData>();
            useAutoScan = false;
            useFullScreenCapture = false;
            useCloudStorage = false;
            useXInput = true;
            isDirty = false;
            cloudToken = null;
        }

        public static PlayerSettingsDB Get()
        {
            return instance;
        }

        public bool Load()
        {
            bool bResult = false;

            string FilePath = AssetManager.Get().CreateFilePath(DBPath);
            if (File.Exists(FilePath))
            {
                using (StreamReader file = new StreamReader(FilePath))
                {
                    string fileContent = file.ReadToEnd();
                    bResult = LoadFromJson(fileContent);
                    file.Close();
                }
            }

            TriadCardDB cardDB = TriadCardDB.Get();
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
            return bResult;
        }

        public bool LoadFromXmlStream(Stream stream)
        {
            TriadCardDB cardDB = TriadCardDB.Get();
            TriadNpcDB npcDB = TriadNpcDB.Get();

            try
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(stream);

                foreach (XmlNode testNode in xdoc.DocumentElement.ChildNodes)
                {
                    try
                    {
                        XmlElement testElem = (XmlElement)testNode;
                        if (testElem != null && testElem.Name == "ui")
                        {
                            int autoScanNum = int.Parse(testElem.GetAttribute("autoScan"));
                            useAutoScan = (autoScanNum == 1);
                        }
                        else if (testElem != null && testElem.Name == "cloud")
                        {
                            int useNum = int.Parse(testElem.GetAttribute("use"));
                            useCloudStorage = (useNum == 1);
                            cloudToken = testElem.HasAttribute("token") ? testElem.GetAttribute("token") : null;
                        }
                        else if (testElem != null && testElem.Name == "card")
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

            return ownedCards.Count > 0;
        }

        public bool LoadFromJson(string jsonStr)
        {
            TriadCardDB cardDB = TriadCardDB.Get();
            TriadNpcDB npcDB = TriadNpcDB.Get();

            try
            {
                JsonParser.ObjectValue jsonOb = JsonParser.ParseJson(jsonStr);

                JsonParser.ObjectValue uiOb = (JsonParser.ObjectValue)jsonOb["ui", null];
                if (uiOb != null)
                {
                    JsonParser.Value BoolTrue = new JsonParser.BoolValue(true);

                    useAutoScan = (JsonParser.BoolValue)uiOb["autoScan", JsonParser.BoolValue.Empty];
                    useFullScreenCapture = (JsonParser.BoolValue)uiOb["forceFSC", JsonParser.BoolValue.Empty];
                    useXInput = (JsonParser.BoolValue)uiOb["xInput", BoolTrue];
                }

                JsonParser.ObjectValue cloudOb = (JsonParser.ObjectValue)jsonOb["cloud", null];
                if (cloudOb != null)
                {
                    useCloudStorage = (JsonParser.BoolValue)cloudOb["use", JsonParser.BoolValue.Empty];
                    cloudToken = (JsonParser.StringValue)cloudOb["token", null];
                }

                JsonParser.ArrayValue cardsArr = (JsonParser.ArrayValue)jsonOb["cards", JsonParser.ArrayValue.Empty];
                foreach (JsonParser.Value value in cardsArr.entries)
                {
                    int cardId = (JsonParser.IntValue)value;
                    ownedCards.Add(cardDB.cards[cardId]);
                }

                JsonParser.ArrayValue npcsArr = (JsonParser.ArrayValue)jsonOb["npcs", JsonParser.ArrayValue.Empty];
                foreach (JsonParser.Value value in npcsArr.entries)
                {
                    int npcId = (JsonParser.IntValue)value;
                    completedNpcs.Add(npcDB.npcs[npcId]);
                }

                JsonParser.ArrayValue decksArr = (JsonParser.ArrayValue)jsonOb["decks", JsonParser.ArrayValue.Empty];
                foreach (JsonParser.Value value in decksArr.entries)
                {
                    JsonParser.ObjectValue deckOb = (JsonParser.ObjectValue)value;
                    int npcId = (JsonParser.IntValue)deckOb["id"];

                    TriadNpc npc = TriadNpcDB.Get().npcs[npcId];
                    if (npc != null)
                    {
                        TriadDeck deckCards = new TriadDeck();

                        cardsArr = (JsonParser.ArrayValue)deckOb["cards", JsonParser.ArrayValue.Empty];
                        foreach (JsonParser.Value cardValue in cardsArr.entries)
                        {
                            int cardId = (JsonParser.IntValue)cardValue;
                            deckCards.knownCards.Add(cardDB.cards[cardId]);
                        }

                        lastDeck.Add(npc, deckCards);
                    }
                }

                JsonParser.ArrayValue favDecksArr = (JsonParser.ArrayValue)jsonOb["favDecks", JsonParser.ArrayValue.Empty];
                foreach (JsonParser.Value value in favDecksArr.entries)
                {
                    JsonParser.ObjectValue deckOb = (JsonParser.ObjectValue)value;
                    TriadDeckNamed deckCards = new TriadDeckNamed();

                    cardsArr = (JsonParser.ArrayValue)deckOb["cards", JsonParser.ArrayValue.Empty];
                    foreach (JsonParser.Value cardValue in cardsArr.entries)
                    {
                        int cardId = (JsonParser.IntValue)cardValue;
                        deckCards.knownCards.Add(cardDB.cards[cardId]);
                    }

                    if (deckCards.knownCards.Count > 0)
                    {
                        deckCards.Name = deckOb["name", JsonParser.StringValue.Empty];
                        favDecks.Add(deckCards);
                    }
                }

                JsonParser.ObjectValue imageHashesOb = (JsonParser.ObjectValue)jsonOb["images", null];
                if (imageHashesOb != null)
                {
                    customHashes = ImageHashDB.Get().LoadImageHashes(imageHashesOb);
                }

                JsonParser.ArrayValue digitHashesArr = (JsonParser.ArrayValue)jsonOb["digits", null];
                if (digitHashesArr != null)
                {
                    customDigits = ImageHashDB.Get().LoadDigitHashes(digitHashesArr);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Loading failed! Exception:" + ex);
            }

            return ownedCards.Count > 0;
        }

        public bool MergeWithContent(string jsonString)
        {
            PlayerSettingsDB mergeDB = new PlayerSettingsDB();
            bool bLoaded = mergeDB.LoadFromJson(jsonString);
            bool bHadUniqueSettings = false;

            if (bLoaded)
            {
                bool bUpdatedOwnedCards = false;
                foreach (TriadCard card in mergeDB.ownedCards)
                {
                    if (!ownedCards.Contains(card))
                    {
                        ownedCards.Add(card);
                        bUpdatedOwnedCards = true;
                    }
                }
                bHadUniqueSettings = bHadUniqueSettings || (ownedCards.Count > mergeDB.ownedCards.Count);

                bool bUpdatedNpcs = false;
                foreach (TriadNpc npc in mergeDB.completedNpcs)
                {
                    if (!completedNpcs.Contains(npc))
                    {
                        completedNpcs.Add(npc);
                        bUpdatedNpcs = true;
                    }
                }
                bHadUniqueSettings = bHadUniqueSettings || (completedNpcs.Count > mergeDB.completedNpcs.Count);

                bool bUpdatedDecks = false;
                foreach (KeyValuePair<TriadNpc, TriadDeck> kvp in mergeDB.lastDeck)
                {
                    if (!lastDeck.ContainsKey(kvp.Key))
                    {
                        lastDeck.Add(kvp.Key, kvp.Value);
                    }

                    // replace existing? skip for now...
                }
                bHadUniqueSettings = bHadUniqueSettings || (lastDeck.Count > mergeDB.lastDeck.Count);

                OnUpdated.Invoke(bUpdatedOwnedCards, bUpdatedNpcs, bUpdatedDecks);
            }

            return bHadUniqueSettings;
        }

        public void Save()
        {
            string FilePath = AssetManager.Get().CreateFilePath(DBPath);
            using (StreamWriter file = new StreamWriter(FilePath))
            {
                string jsonString = SaveToJson(false);
                file.Write(jsonString);
                file.Close();
            }
        }

        public string SaveToString()
        {
            isDirty = false;
            return SaveToJson(true);
        }

        public string SaveToJson(bool bLimitedMode = false)
        {
            JsonWriter jsonWriter = new JsonWriter();
            try
            {
                jsonWriter.WriteObjectStart();

                if (!bLimitedMode)
                {
                    jsonWriter.WriteObjectStart("ui");
                    jsonWriter.WriteBool(useAutoScan, "autoScan");
                    jsonWriter.WriteBool(useFullScreenCapture, "forceFSC");
                    jsonWriter.WriteBool(useXInput, "xInput");

                    jsonWriter.WriteObjectEnd();
                }

                if (!bLimitedMode)
                {
                    jsonWriter.WriteObjectStart("cloud");
                    jsonWriter.WriteBool(useCloudStorage, "use");
                    if (cloudToken != null)
                    {
                        jsonWriter.WriteString(cloudToken, "token");
                    }

                    jsonWriter.WriteObjectEnd();
                }

                {
                    List<int> listIds = new List<int>();
                    foreach (TriadCard card in ownedCards)
                    {
                        listIds.Add(card.Id);
                    }
                    listIds.Sort();

                    jsonWriter.WriteArrayStart("cards");
                    foreach (int id in listIds)
                    {
                        jsonWriter.WriteInt(id);
                    }

                    jsonWriter.WriteArrayEnd();
                }

                {
                    List<int> listIds = new List<int>();
                    foreach (TriadNpc npc in completedNpcs)
                    {
                        listIds.Add(npc.Id);
                    }
                    listIds.Sort();

                    jsonWriter.WriteArrayStart("npcs");
                    foreach (int id in listIds)
                    {
                        jsonWriter.WriteInt(id);
                    }

                    jsonWriter.WriteArrayEnd();
                }

                {
                    jsonWriter.WriteArrayStart("decks");
                    foreach (KeyValuePair<TriadNpc, TriadDeck> kvp in lastDeck)
                    {
                        jsonWriter.WriteObjectStart();
                        jsonWriter.WriteInt(kvp.Key.Id, "id");
                        jsonWriter.WriteArrayStart("cards");
                        for (int Idx = 0; Idx < kvp.Value.knownCards.Count; Idx++)
                        {
                            jsonWriter.WriteInt(kvp.Value.knownCards[Idx].Id);
                        }
                        jsonWriter.WriteArrayEnd();
                        jsonWriter.WriteObjectEnd();
                    }

                    jsonWriter.WriteArrayEnd();
                }

                {
                    jsonWriter.WriteArrayStart("favDecks");
                    foreach (TriadDeckNamed deck in favDecks)
                    {
                        jsonWriter.WriteObjectStart();
                        if (deck != null)
                        {
                            jsonWriter.WriteString(deck.Name, "name");
                            jsonWriter.WriteArrayStart("cards");
                            for (int Idx = 0; Idx < deck.knownCards.Count; Idx++)
                            {
                                jsonWriter.WriteInt(deck.knownCards[Idx].Id);
                            }
                            jsonWriter.WriteArrayEnd();
                        }
                        jsonWriter.WriteObjectEnd();
                    }
                    jsonWriter.WriteArrayEnd();
                }

                if (!bLimitedMode)
                {
                    jsonWriter.WriteObjectStart("images");
                    ImageHashDB.Get().StoreImageHashes(customHashes, jsonWriter);
                    jsonWriter.WriteObjectEnd();

                    jsonWriter.WriteArrayStart("digits");
                    ImageHashDB.Get().StoreDigitHashes(customDigits, jsonWriter);
                    jsonWriter.WriteArrayEnd();
                }

                jsonWriter.WriteObjectEnd();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Saving failed! Exception:" + ex);
            }

            return jsonWriter.ToString();
        }

        public void MarkDirty()
        {
            isDirty = true;
        }

        public void UpdatePlayerDeckForNpc(TriadNpc npc, TriadDeck deck)
        {
            if (npc != null && deck != null && deck.knownCards.Count == 5)
            {
                bool bIsStarterDeck = true;
                for (int Idx = 0; Idx < starterCards.Length; Idx++)
                {
                    bIsStarterDeck = bIsStarterDeck && (starterCards[Idx] == deck.knownCards[Idx]);
                }

                if (!bIsStarterDeck)
                {
                    bool bChanged = true;
                    if (lastDeck.ContainsKey(npc))
                    {
                        if (lastDeck[npc].Equals(deck))
                        {
                            bChanged = false;
                        }
                        else
                        {
                            lastDeck.Remove(npc);
                        }
                    }

                    if (bChanged)
                    {
                        TriadCard[] deckCardsCopy = deck.knownCards.ToArray();
                        lastDeck.Add(npc, new TriadDeck(deckCardsCopy));
                        MarkDirty();
                    }
                }
            }
        }

        public void UpdateFavDeck(int slot, TriadDeckNamed deck)
        {
            if (slot < 0 || slot > 16)
            {
                return;
            }

            if (deck == null)
            {
                if (slot < favDecks.Count)
                {
                    favDecks.RemoveAt(slot);
                }

                MarkDirty();
            }
            else
            {
                while (favDecks.Count <= slot)
                {
                    favDecks.Add(null);
                }

                bool bChanged = (deck == null) != (favDecks[slot] == null);
                if (!bChanged && (deck != null))
                {
                    bChanged = !deck.Name.Equals(favDecks[slot].Name) || (deck.knownCards != favDecks[slot].knownCards);
                }

                if (bChanged)
                {
                    MarkDirty();
                }

                favDecks[slot] = deck;
            }
        }

        public void AddLockedHash(ImageHashData hashData)
        {
            if (!IsLockedHash(hashData))
            {
                for (int Idx = 0; Idx < customHashes.Count; Idx++)
                {
                    customHashes[Idx].IsHashMatching(hashData, out int testDistance);

                    // exact match only
                    if (testDistance == 0)
                    {
                        customHashes.RemoveAt(Idx);
                        MarkDirty();
                        break;
                    }
                }

                lockedHashes.Add(hashData);
            }
        }

        public bool IsLockedHash(HashCollection hash)
        {
            foreach (ImageHashData testData in lockedHashes)
            {
                testData.IsHashMatching(hash, out int testDistance);

                // exact match only
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
                lockedHashes[Idx].IsHashMatching(hashData, out int testDistance);

                // exact match only
                if (testDistance == 0)
                {
                    lockedHashes.RemoveAt(Idx);
                    break;
                }
            }

            customHashes.Add(hashData);
            MarkDirty();
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
            MarkDirty();
        }
    }
}
