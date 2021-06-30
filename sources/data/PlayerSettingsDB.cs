using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace FFTriadBuddy
{
    public class PlayerSettingsDB
    {
        public List<TriadCard> ownedCards;
        public List<TriadNpc> completedNpcs;
        public List<ImageHashData> customHashes;
        public TriadCard[] starterCards;
        public Dictionary<TriadNpc, TriadDeck> lastDeck;
        public List<TriadDeckNamed> favDecks;
        public bool useAutoScan;
        public bool useFullScreenCapture;
        public bool useCloudStorage;
        public bool useXInput;
        public bool useBigUI;
        public bool isDirty;
        public string DBPath;
        public string cloudToken;
        public string forcedLanguage;

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
            useAutoScan = false;
            useFullScreenCapture = false;
            useCloudStorage = false;
            useXInput = true;
            useBigUI = false;
            isDirty = false;
            cloudToken = null;
            forcedLanguage = null;
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
                    JsonParser.Value BoolFalse = new JsonParser.BoolValue(false);

                    useAutoScan = (JsonParser.BoolValue)uiOb["autoScan", JsonParser.BoolValue.Empty];
                    useFullScreenCapture = (JsonParser.BoolValue)uiOb["forceFSC", JsonParser.BoolValue.Empty];
                    useXInput = (JsonParser.BoolValue)uiOb["xInput", BoolTrue];
                    useBigUI = (JsonParser.BoolValue)uiOb["bigUI", BoolFalse];
                    forcedLanguage = (JsonParser.StringValue)uiOb["lang", null];
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
                    ImageHashDB.Get().hashes.AddRange(customHashes);
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
                    jsonWriter.WriteBool(useBigUI, "bigUI");
                    jsonWriter.WriteString(forcedLanguage, "lang");

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
                    ImageHashDB.Get().StoreHashes(customHashes, jsonWriter);
                    jsonWriter.WriteObjectEnd();
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

        public void AddKnownHash(ImageHashData hashData)
        {
            foreach (ImageHashData testHash in customHashes)
            {
                if (hashData.IsMatching(testHash, 0, out int dummyDistance))
                {
                    Logger.WriteLine("Adding hash ({0}: {1}) failed! Colision with already known ({2}: {3})", hashData.type, hashData.ownerOb, testHash.type, testHash.ownerOb);
                    return;
                }
            }

            customHashes.Add(hashData);
            ImageHashDB.Get().hashes.Add(hashData);

            MarkDirty();
        }

        public void RemoveKnownHash(ImageHashData hashData)
        {
            for (int idx = customHashes.Count - 1; idx >= 0; idx--)
            {
                ImageHashData testHash = customHashes[idx];
                if (hashData.IsMatching(testHash, 0, out int dummyDistance))
                {
                    customHashes.RemoveAt(idx);
                    ImageHashDB.Get().hashes.Remove(testHash);

                    MarkDirty();
                }
            }
        }
    }
}
