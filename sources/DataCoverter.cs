using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    class MapData
    {
        public string Name = null;
        public string MapCode = null;
        public float Scale = 0.0f;
        public float OffsetX = 0.0f;
        public float OffsetY = 0.0f;

        public string GetLocationDesc(float X, float Y)
        {
            int MapX = CovertCoordTo2d(X, OffsetX);
            int MapY = CovertCoordTo2d(Y, OffsetY);
            return Name + " (" + MapX + ", " + MapY + ")";
        }

        private int CovertCoordTo2d(float Coord, float Offset)
        {
            float useScale = Scale / 100.0f;
            float useValue = (Coord + Offset) * useScale;
            return (int)Math.Round(((41.0 / useScale) * ((useValue + 1024.0) / 2048.0)) + 1);
        }

        public override string ToString()
        {
            return MapCode + ":" + Name;
        }
    }

    class DataConverter
    {
        public void Run()
        {
#if DEBUG
            string folderPath = @"..\..\..\datasource\export\exd\";

            Logger.WriteLine("Parsing card data...");
            Dictionary<int, TriadCard> triadCards = ParseCards(folderPath);
            bool hasCardChanges = UpdateCards(triadCards);

            Logger.WriteLine("Parsing npc data...");
            Dictionary<int, TriadNpc> triadNpcs = ParseNpcs(folderPath, triadCards);
            bool hasNpcChanges = UpdateNpcs(triadNpcs);

            Logger.WriteLine("Done! " + ((hasCardChanges || hasNpcChanges) ? @"Database saved in assets\data\*.xml.new files" : "No changes detected, database is up to date"));
#endif // DEBUG
        }

#if DEBUG
        private string[] SplitCSVRow(string csvRow)
        {
            List<string> tokens = new List<string>();
            int sepIdx = csvRow.IndexOf(',');
            int lastSep = -1;
            string prevToken = "";
            bool waitingForString = false;

            while (sepIdx >= 0)
            {
                string token = (sepIdx < 1) ? "" : csvRow.Substring(lastSep + 1, sepIdx - lastSep - 1);
                lastSep = sepIdx;

                if (waitingForString)
                {
                    prevToken += "," + token;

                    if (token.EndsWith("\""))
                    {
                        waitingForString = false;
                        prevToken = prevToken.Substring(1, prevToken.Length - 2);

                        tokens.Add(prevToken);
                    }
                }
                else
                {
                    if (token.Length > 0 && token[0] == '"' && !token.EndsWith("\""))
                    {
                        prevToken = token;
                        waitingForString = true;
                    }
                    else
                    {
                        if (token.Length > 0 && token[0] == '"')
                        {
                            token = (token.Length > 2) ? token.Substring(1, token.Length - 2) : "";
                        }

                        tokens.Add(token);
                    }
                }

                sepIdx = csvRow.IndexOf(',', lastSep + 1);
            }

            string lastToken = (lastSep < 0) ? csvRow : csvRow.Substring(lastSep + 1);
            if (lastToken.Length > 0 && lastToken[0] == '"')
            {
                lastToken = lastToken.Substring(1, lastToken.Length - 2);
            }

            tokens.Add(lastToken);

            return tokens.ToArray();
        }

        private List<string[]> ParseCSVFile(string path, int numRowsToSkip = 2)
        {
            List<string[]> data = new List<string[]>();
            int lineIdx = 0;
            int skipCounter = numRowsToSkip;

            using (StreamReader reader = new StreamReader(path))
            {
                string header = reader.ReadLine();
                int numCols = header.Split(',').Length;
                lineIdx++;

                while (!reader.EndOfStream)
                {
                    string row = reader.ReadLine();
                    lineIdx++;
                    if (skipCounter > 0)
                    {
                        skipCounter--;
                        continue;
                    }

                    if (row.Length > 0)
                    {
                        string[] cols = SplitCSVRow(row);
                        if (cols.Length != numCols)
                        {
                            continue;
                            //throw new Exception("Column count mismatch at '" + path + "' line:" + lineIdx);
                        }

                        data.Add(cols);
                    }
                }
            }

            return data;
        }

        private Dictionary<string, ETriadCardType> CreateCardTypeNameMap()
        {
            Dictionary<string, ETriadCardType> nameMap = new Dictionary<string, ETriadCardType>();
            string[] enumNames = Enum.GetNames(typeof(ETriadCardType));
            for (int enumIdx = 0; enumIdx < enumNames.Length; enumIdx++)
            {
                nameMap.Add(enumNames[enumIdx], (ETriadCardType)enumIdx);
            }

            return nameMap;
        }

        private Dictionary<int, ETriadCardType> ParseCardTypes(string folderPath)
        {
            Dictionary<int, ETriadCardType> typeMap = new Dictionary<int, ETriadCardType>();
            typeMap.Add(0, ETriadCardType.None);

            List<string[]> cardTypes = ParseCSVFile(folderPath + "TripleTriadCardType.csv");
            if (cardTypes.Count > 0 && cardTypes[0].Length == 2)
            {
                string[] enumNames = Enum.GetNames(typeof(ETriadCardType));
                for (int enumIdx = 0; enumIdx < enumNames.Length; enumIdx++)
                {
                    bool assigned = false;
                    for (int Idx = 0; Idx < cardTypes.Count; Idx++)
                    {
                        if (cardTypes[Idx][1] == enumNames[enumIdx])
                        {
                            typeMap.Add(int.Parse(cardTypes[Idx][0]), (ETriadCardType)enumIdx);
                            assigned = true;
                            break;
                        }
                    }

                    if (!assigned && enumIdx != 0)
                    {
                        throw new Exception("Unable to parse card types from csv: missing card type '" + enumNames[enumIdx] + "' [1]");
                    }
                }

                for (int Idx = 0; Idx < cardTypes.Count; Idx++)
                {
                    if (cardTypes[Idx][1].Length > 0)
                    {
                        bool bFound = enumNames.Contains(cardTypes[Idx][1]);
                        if (!bFound)
                        {
                            throw new Exception("Unable to parse card types from csv: missing card type '" + cardTypes[Idx][1] + "' [2]");
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse card types from csv!");
            }

            return typeMap;
        }

        private Dictionary<int, string> ParseCardNames(string folderPath)
        {
            Dictionary<int, string> nameMap = new Dictionary<int, string>();

            List<string[]> cardNames = ParseCSVFile(folderPath + "TripleTriadCard.csv");
            if (cardNames.Count > 0 && cardNames[0].Length == 10)
            {
                for (int Idx = 0; Idx < cardNames.Count; Idx++)
                {
                    if (cardNames[Idx][1].Length > 0)
                    {
                        nameMap.Add(int.Parse(cardNames[Idx][0]), cardNames[Idx][1]);
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse card names from csv!");
            }

            return nameMap;
        }

        private Dictionary<int, TriadCard> ParseCards(string folderPath)
        {
            Dictionary<int, ETriadCardType> typeMap = ParseCardTypes(folderPath);
            Dictionary<int, string> nameMap = ParseCardNames(folderPath);
            Dictionary<string, ETriadCardType> typeNameMap = CreateCardTypeNameMap(); 

            string patternRarity = "TripleTriadCardRarity#";

            Dictionary<int, TriadCard> loadedCards = new Dictionary<int, TriadCard>();
            List<string[]> cardData = ParseCSVFile(folderPath + "TripleTriadCardResident.csv");
            if (cardData.Count > 0 && cardData[0].Length == 17)
            {
                for (int Idx = 0; Idx < cardData.Count; Idx++)
                {
                    int keyIdx = int.Parse(cardData[Idx][0]);

                    int rarityIdx = 0;
                    if (cardData[Idx][6].StartsWith(patternRarity))
                    {
                        rarityIdx = int.Parse(cardData[Idx][6].Substring(patternRarity.Length));
                    }
                    else
                    {
                        rarityIdx = int.Parse(cardData[Idx][6]);
                    }

                    ETriadCardType cardType = ETriadCardType.None;
                    string typeDef = cardData[Idx][7];
                    if (typeDef.Length == 1)
                    {
                        int typeDefInt = int.Parse(typeDef);
                        cardType = typeMap[typeDefInt];
                    }
                    else if (typeDef.Length > 0)
                    {
                        cardType = typeNameMap[typeDef];
                    }

                    int iconId = 82500 + keyIdx;
                    string iconPath = iconId.ToString("000000") + ".png";

                    int numUp = int.Parse(cardData[Idx][2]);
                    int numDown = int.Parse(cardData[Idx][3]);
                    int numRight = int.Parse(cardData[Idx][4]);
                    int numLeft = int.Parse(cardData[Idx][5]);

                    if (numUp > 0 && numDown > 0 && numRight > 0 && numLeft > 0)
                    {
                        TriadCard cardOb = new TriadCard(0, nameMap[keyIdx], iconPath,
                            (ETriadCardRarity)(rarityIdx - 1), cardType, 
                            numUp, numDown, numLeft, numRight, 
                            int.Parse(cardData[Idx][10]),
                            int.Parse(cardData[Idx][11]));

                        if (cardOb.IsValid())
                        {
                            loadedCards.Add(keyIdx, cardOb);
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse cards from csv!");
            }

            return loadedCards;
        }

        private bool UpdateCards(Dictionary<int, TriadCard> cardMap)
        {
            TriadCardDB cardDB = TriadCardDB.Get();
            int newCardId = cardDB.cards[cardDB.cards.Count - 1].Id + 1;
            bool hasChanges = false;

            foreach (KeyValuePair<int, TriadCard> kvp in cardMap)
            {
                if (kvp.Key > 285)
                {
                    int a = 1;
                    a++;
                }

                if (kvp.Key >= newCardId)
                {
                    kvp.Value.Id = kvp.Key;
                    newCardId = kvp.Key + 1;

                    while (cardDB.cards.Count < newCardId)
                    {
                        cardDB.cards.Add(null);
                    }
                    cardDB.cards[kvp.Value.Id] = kvp.Value;

                    Logger.WriteLine("Newly added card: " + kvp.Value.ToShortString());
                    hasChanges = true;
                    continue;
                }

                TriadCard cardMatch = cardDB.Find(kvp.Value.Name);
                if (cardMatch == null)
                {
                    foreach (TriadCard testCard in cardDB.cards)
                    {
                        if (testCard != null &&
                            testCard.Rarity == kvp.Value.Rarity &&
                            testCard.Type == kvp.Value.Type &&
                            testCard.Sides[0] == kvp.Value.Sides[0] &&
                            testCard.Sides[1] == kvp.Value.Sides[1] &&
                            testCard.Sides[2] == kvp.Value.Sides[2] &&
                            testCard.Sides[3] == kvp.Value.Sides[3])
                        {
                            Logger.WriteLine("Card name remap: " + testCard.ToShortString() + " => " + kvp.Value.Name);
                            cardMatch = testCard;
                            hasChanges = true;
                            break;
                        }
                    }
                }

                if (cardMatch != null)
                { 
                    kvp.Value.Id = cardMatch.Id;
                    cardDB.cards[kvp.Value.Id] = kvp.Value;
                }
                else
                {
                    TriadCard otherCard = cardDB.cards[kvp.Key];
                    Logger.WriteLine("Missing card match: " + kvp.Value.ToString() + "! Will replace existing Id: " + (otherCard != null ? otherCard.ToString() : "??"));

                    kvp.Value.Id = kvp.Key;
                    cardDB.cards[kvp.Value.Id] = kvp.Value;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                cardDB.Save();
            }

            return hasChanges;
        }

        private Dictionary<int, TriadGameModifier> ParseGameRules(string folderPath)
        {
            Dictionary<int, TriadGameModifier> ruleMap = new Dictionary<int, TriadGameModifier>();
            List<string[]> cardRules = ParseCSVFile(folderPath + "TripleTriadRule.csv");
            if (cardRules.Count > 0 && cardRules[0].Length == 8)
            {
                List<TriadGameModifier> modList = new List<TriadGameModifier>();
                foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
                {
                    if (type.IsSubclassOf(typeof(TriadGameModifier)))
                    {
                        TriadGameModifier modInst = (TriadGameModifier)Activator.CreateInstance(type);
                        modList.Add(modInst);
                    }
                }

                for (int modIdx = 0; modIdx < modList.Count; modIdx++)
                {
                    bool assigned = false;
                    for (int Idx = 0; Idx < cardRules.Count; Idx++)
                    {
                        if (cardRules[Idx][1] == modList[modIdx].GetName())
                        {
                            ruleMap.Add(int.Parse(cardRules[Idx][0]), modList[modIdx]);
                            assigned = true;
                            break;
                        }
                    }

                    if (!assigned && modList[modIdx].GetName() != "None")
                    {
                        throw new Exception("Unable to parse types from csv: missing rule type '" + modList[modIdx].GetName() + "' [1]");
                    }
                }

                for (int Idx = 0; Idx < cardRules.Count; Idx++)
                {
                    if (cardRules[Idx][1].Length > 0)
                    {
                        bool bFound = false;
                        for (int modIdx = 0; modIdx < modList.Count && !bFound; modIdx++)
                        {
                            bFound = modList[modIdx].GetName() == cardRules[Idx][1];
                        }

                        if (!bFound)
                        {
                            throw new Exception("Unable to parse types from csv: missing rule type '" + cardRules[Idx][1] + "' [2]");
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse rules from csv!");
            }

            return ruleMap;
        }

        private Dictionary<int, string> ParseNpcNames(string folderPath)
        {
            Dictionary<int, string> nameMap = new Dictionary<int, string>();
            List<string[]> npcData = ParseCSVFile(folderPath + "ENpcResident.csv");
            if (npcData.Count > 0 && npcData[0].Length == 12)
            {
                for (int Idx = 0; Idx < npcData.Count; Idx++)
                {
                    if (npcData[Idx][1].Length > 0)
                    {
                        nameMap.Add(int.Parse(npcData[Idx][0]), npcData[Idx][1]);
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse npc names from csv!");
            }

            return nameMap;
        }

        private Dictionary<int, int> ParseNpcTriadIds(string folderPath)
        {
            Dictionary<int, int> idMap = new Dictionary<int, int>();
            List<string[]> npcData = ParseCSVFile(folderPath + "TripleTriadResident.csv");
            if (npcData.Count > 0 && npcData[0].Length == 2)
            {
                for (int Idx = 0; Idx < npcData.Count; Idx++)
                {
                    if (npcData[Idx][1] != "65535")
                    {
                        idMap.Add(int.Parse(npcData[Idx][0]), int.Parse(npcData[Idx][1]));
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse npc ids from csv! [1]");
            }

            return idMap;
        }

        private Dictionary<int, int> ParseNpcTriadNpcIds(string folderPath, Dictionary<int, int> triadNpcIds)
        {
            List<string> allNpcKeys = new List<string>();
            foreach (KeyValuePair<int, int> kvp in triadNpcIds)
            {
                allNpcKeys.Add(kvp.Key.ToString());
            }

            string pattern = "TripleTriad#";

            Dictionary<int, int> idMap = new Dictionary<int, int>();
            List<string[]> npcData = ParseCSVFile(folderPath + "ENpcBase.csv");
            if (npcData.Count > 0 && npcData[0].Length >= 8)
            {
                for (int Idx = 0; Idx < npcData.Count; Idx++)
                {
                    for (int FieldIdx = 3; FieldIdx <= 33; FieldIdx++)
                    {
                        string fieldStr = npcData[Idx][FieldIdx];
                        if (fieldStr.StartsWith(pattern))
                        {
                            fieldStr = fieldStr.Substring(pattern.Length);
                        }

                        if (allNpcKeys.Contains(fieldStr))
                        {
                            int fieldInt = int.Parse(fieldStr);
                            if (!idMap.ContainsKey(fieldInt))
                            {
                                idMap.Add(fieldInt, int.Parse(npcData[Idx][0]));
                            }

                            break;
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse npc ids from csv! [2]");
            }

            return idMap;
        }

        private Dictionary<int, int> ParseRewardItems(string folderPath, Dictionary<int, TriadCard> cardMap)
        {
            Dictionary<int, int> idMap = new Dictionary<int, int>();
            List<string[]> itemData = ParseCSVFile(folderPath + "Item.csv");
            if (itemData.Count > 0 && itemData[0].Length == 90)
            {
                for (int Idx = 0; Idx < itemData.Count; Idx++)
                {
                    if (itemData[Idx][16] == "Triple Triad Card")
                    {
                        string findName = itemData[Idx][15];
                        bool bHasMatch = false;
                        foreach (KeyValuePair<int, TriadCard> kvp in cardMap)
                        {
                            if (kvp.Value.Name.Equals(findName))
                            {
                                idMap.Add(int.Parse(itemData[Idx][0]), kvp.Key);
                                bHasMatch = true;
                                break;
                            }
                        }

                        if (!bHasMatch)
                        {
                            throw new Exception("Unable to parse rewards from csv! No match for: " + findName);
                        }
                    }
                    else
                    {
                        bool IsCardItem = itemData[Idx][1].Contains("card");
                        if (IsCardItem && itemData[Idx][15].Length > 0 && itemData[Idx][15] != "0")
                        {
                            bool bParsed = int.TryParse(itemData[Idx][15], out int foundItemId);
                            if (bParsed)
                            {
                                idMap.Add(int.Parse(itemData[Idx][0]), foundItemId);
                            }
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse rewards from csv!");
            }

            return idMap;
        }

        private Dictionary<string, MapData> ParseMapData(string folderPath)
        {
            Dictionary<string, MapData> maps = new Dictionary<string, MapData>();
            List<string[]> mapData = ParseCSVFile(folderPath + "Map.csv");
            if (mapData.Count > 0 && mapData[0].Length == 19)
            {
                for (int Idx = 0; Idx < mapData.Count; Idx++)
                {
                    string mapCode = mapData[Idx][7];
                    if (mapCode.Length > 0 && !mapCode.StartsWith("default"))
                    {
                        MapData newMapData = new MapData();
                        newMapData.MapCode = mapCode;
                        newMapData.Scale = int.Parse(mapData[Idx][8]);
                        newMapData.OffsetX = int.Parse(mapData[Idx][9]);
                        newMapData.OffsetY = int.Parse(mapData[Idx][10]);
                        newMapData.Name = mapData[Idx][12];

                        if (!maps.ContainsKey(mapCode))
                        {
                            maps.Add(mapCode, newMapData);
                        }
                        else
                        {
                            MapData oldMapData = maps[mapCode];
                            bool hasChanges = (oldMapData.Scale != newMapData.Scale) || (oldMapData.OffsetX != newMapData.OffsetX) || (oldMapData.OffsetY != newMapData.OffsetY);
                            if (hasChanges)
                            {
                                Logger.WriteLine("Duplicate map code found: " + newMapData + ", using prev. Scale:" +
                                    oldMapData.Scale + "<>" + newMapData.Scale + ", OffsetX:" +
                                    oldMapData.OffsetX + "<>" + newMapData.OffsetX + ", OffsetY:" +
                                    oldMapData.OffsetY + "<>" + newMapData.OffsetY + ".");
                            }
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse map from csv!");
            }

            return maps;
        }

        private Dictionary<int, string> ParseNpcLocations(string folderPath, List<int> npcIds)
        {
            Dictionary<string, MapData> locationMap = ParseMapData(folderPath);
            string pattern = "ENpcBase#";

            Dictionary<int, string> npcLocationMap = new Dictionary<int, string>();
            List<string[]> posData = ParseCSVFile(folderPath + "Level.csv");
            if (posData.Count > 0 && posData[0].Length == 11)
            {
                for (int Idx = 0; Idx < posData.Count; Idx++)
                {
                    if (posData[Idx][6] == "8" && posData[Idx][7].Length > 0)
                    {
                        int npcId = posData[Idx][7].StartsWith(pattern) ? int.Parse(posData[Idx][7].Substring(pattern.Length)) : int.Parse(posData[Idx][7]);
                        if (npcIds.Contains(npcId))
                        {
                            float posX = float.Parse(posData[Idx][1], CultureInfo.InvariantCulture);
                            float posZ = float.Parse(posData[Idx][3], CultureInfo.InvariantCulture);
                            string mapCode = posData[Idx][8];

                            MapData useMap = locationMap[mapCode];
                            string locationDesc = useMap.GetLocationDesc(posX, posZ);
                            npcLocationMap.Add(npcId, locationDesc);
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Unable to parse npc locations from csv!");
            }

            return npcLocationMap;
        }

        private Dictionary<string, TriadCard> BuildCardNameMap(Dictionary<int, TriadCard> cardMap)
        {
            Dictionary<string, TriadCard> nameMap = new Dictionary<string, TriadCard>();
            foreach (KeyValuePair<int, TriadCard> kvp in cardMap)
            {
                nameMap.Add(kvp.Value.Name, kvp.Value);
            }

            return nameMap;
        }

        private Dictionary<string, TriadGameModifier> BuildRuleNameMap()
        {
            Dictionary<string, TriadGameModifier> ruleMap = new Dictionary<string, TriadGameModifier>();

            foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
            {
                if (type.IsSubclassOf(typeof(TriadGameModifier)))
                {
                    TriadGameModifier modInst = (TriadGameModifier)Activator.CreateInstance(type);
                    ruleMap.Add(modInst.GetName(), modInst);
                }
            }

            return ruleMap;
        }

        private Dictionary<int, TriadNpc> ParseNpcs(string folderPath, Dictionary<int, TriadCard> cardMap)
        {
            int DataFormat = 2;

            Dictionary<int, TriadGameModifier> ruleMap = ParseGameRules(folderPath);
            Dictionary<int, string> npcNameMap = ParseNpcNames(folderPath);
            Dictionary<int, int> npcTriadIdMap = ParseNpcTriadIds(folderPath);
            Dictionary<int, int> npcBaseIdMap = ParseNpcTriadNpcIds(folderPath, npcTriadIdMap);
            Dictionary<int, string> npcLocationMap = ParseNpcLocations(folderPath, npcBaseIdMap.Values.ToList());

            Dictionary<int, int> rewardMap = (DataFormat == 1) ? ParseRewardItems(folderPath, cardMap) : null;
            Dictionary<string, TriadCard> cardNameMap = (DataFormat == 2) ? BuildCardNameMap(cardMap) : null;
            Dictionary<string, TriadGameModifier> ruleNameMap = (DataFormat == 2) ? BuildRuleNameMap() : null;

            Dictionary<int, TriadNpc> loadedNpcs = new Dictionary<int, TriadNpc>();
            List<string[]> npcData = ParseCSVFile(folderPath + "TripleTriad.csv");
            if (npcData.Count > 0 && npcData[0].Length == 31)
            {
                for (int Idx = 0; Idx < npcData.Count; Idx++)
                {
                    int npcKey = int.Parse(npcData[Idx][0]);
                    if (npcKey <= 0 || !npcBaseIdMap.ContainsKey(npcKey))
                    {
                        continue;
                    }

                    int npcBaseId = npcBaseIdMap[npcKey];
                    int npcId = npcTriadIdMap[npcKey];

                    List<int> cardsFixed = new List<int>();
                    List<int> cardsVar = new List<int>();

                    if (DataFormat == 1)
                    {
                        cardsFixed.Add(int.Parse(npcData[Idx][1]));
                        cardsFixed.Add(int.Parse(npcData[Idx][2]));
                        cardsFixed.Add(int.Parse(npcData[Idx][3]));
                        cardsFixed.Add(int.Parse(npcData[Idx][4]));
                        cardsFixed.Add(int.Parse(npcData[Idx][5]));
                        for (int cardIdx = cardsFixed.Count - 1; cardIdx >= 0; cardIdx--)
                        {
                            if (cardsFixed[cardIdx] == 0)
                            {
                                cardsFixed.RemoveAt(cardIdx);
                            }
                            else
                            {
                                cardsFixed[cardIdx] = cardMap[cardsFixed[cardIdx]].Id;
                            }
                        }

                        cardsVar.Add(int.Parse(npcData[Idx][6]));
                        cardsVar.Add(int.Parse(npcData[Idx][7]));
                        cardsVar.Add(int.Parse(npcData[Idx][8]));
                        cardsVar.Add(int.Parse(npcData[Idx][9]));
                        cardsVar.Add(int.Parse(npcData[Idx][10]));
                        for (int cardIdx = cardsVar.Count - 1; cardIdx >= 0; cardIdx--)
                        {
                            if (cardsVar[cardIdx] == 0)
                            {
                                cardsVar.RemoveAt(cardIdx);
                            }
                            else
                            {
                                cardsVar[cardIdx] = cardMap[cardsVar[cardIdx]].Id;
                            }
                        }
                    }
                    else
                    {
                        for (int InnerIdx = 1; InnerIdx <= 5; InnerIdx++)
                        {
                            if (npcData[Idx][InnerIdx].Length > 0 && npcData[Idx][InnerIdx] != "0")
                            {
                                TriadCard matchingCard = cardNameMap[npcData[Idx][InnerIdx]];
                                cardsFixed.Add(matchingCard.Id);
                            }
                        }

                        for (int InnerIdx = 6; InnerIdx <= 10; InnerIdx++)
                        {
                            if (npcData[Idx][InnerIdx].Length > 0 && npcData[Idx][InnerIdx] != "0")
                            {
                                TriadCard matchingCard = cardNameMap[npcData[Idx][InnerIdx]];
                                cardsVar.Add(matchingCard.Id);
                            }
                        }
                    }

                    if (cardsFixed.Count == 0 && cardsVar.Count == 0)
                    {
                        continue;
                    }

                    List<TriadGameModifier> rules = new List<TriadGameModifier>();
                    if (DataFormat == 1)
                    {
                        int rule1 = int.Parse(npcData[Idx][11]);
                        if (rule1 != 0) { rules.Add(ruleMap[rule1]); }
                        int rule2 = int.Parse(npcData[Idx][12]);
                        if (rule2 != 0) { rules.Add(ruleMap[rule2]); }
                    }
                    else
                    {
                        if (npcData[Idx][11].Length > 0) { rules.Add(ruleNameMap[npcData[Idx][11]]); }
                        if (npcData[Idx][12].Length > 0) { rules.Add(ruleNameMap[npcData[Idx][12]]); }
                    }

                    List<TriadCard> rewardCards = new List<TriadCard>();
                    if (DataFormat == 1)
                    {
                        int[] rewardItems = new int[4] { int.Parse(npcData[Idx][27]), int.Parse(npcData[Idx][28]), int.Parse(npcData[Idx][29]), int.Parse(npcData[Idx][30]) };
                        foreach (int itemId in rewardItems)
                        {
                            if (itemId > 0)
                            {
                                int cardId = rewardMap[itemId];
                                TriadCard cardOb = cardMap[cardId];
                                rewardCards.Add(cardOb);
                            }
                        }
                    }
                    else
                    {
                        for (int InnerIdx = 27; InnerIdx <= 30; InnerIdx++)
                        {
                            if (npcData[Idx][InnerIdx].Length > 0 && npcData[Idx][InnerIdx] != "0")
                            {
                                string cardName = npcData[Idx][InnerIdx];
                                if (cardName.EndsWith(" Card"))
                                {
                                    cardName = cardName.Remove(cardName.Length - 5);
                                }

                                if (!cardNameMap.ContainsKey(cardName))
                                {
                                    cardName = "The " + cardName;
                                }

                                TriadCard matchingCard = cardNameMap[cardName];
                                rewardCards.Add(matchingCard);
                            }
                        }
                    }

                    string npcDesc = npcNameMap[npcBaseId];
                    string locationDesc = npcLocationMap[npcBaseId];

                    TriadNpc npcOb = new TriadNpc(0, npcDesc, locationDesc, rules, rewardCards, cardsFixed.ToArray(), cardsVar.ToArray());
                    loadedNpcs.Add(npcId, npcOb);
                }
            }
            else
            {
                throw new Exception("Unable to parse npcs from csv!");
            }

            return loadedNpcs;
        }

        private bool HasMatchingRules(List<TriadGameModifier> list1, List<TriadGameModifier> list2)
        {
            int NumMatching = 0;
            if (list1.Count == list2.Count)
            {
                for (int Idx = 0; Idx < list1.Count; Idx++)
                {
                    for (int IdxInner = 0; IdxInner < list2.Count; IdxInner++)
                    {
                        if (list1[Idx].GetType() == list2[IdxInner].GetType())
                        {
                            NumMatching++;
                            break;
                        }
                    }
                }
            }

            return NumMatching == list1.Count;
        }

        private bool HasMatchingRewards(List<TriadCard> list1, List<TriadCard> list2)
        {
            int NumMatching = 0;
            if (list1.Count == list2.Count)
            {
                for (int Idx = 0; Idx < list1.Count; Idx++)
                {
                    for (int IdxInner = 0; IdxInner < list2.Count; IdxInner++)
                    {
                        if (list1[Idx].Id == list2[IdxInner].Id)
                        {
                            NumMatching++;
                            break;
                        }
                    }
                }
            }

            return NumMatching == list1.Count;
        }

        private bool UpdateNpcs(Dictionary<int, TriadNpc> npcMap)
        {
            TriadNpcDB npcDB = TriadNpcDB.Get();
            int newNpcId = npcDB.npcs[npcDB.npcs.Count - 1].Id + 1;
            bool hasChanges = false;

            foreach (KeyValuePair<int, TriadNpc> kvp in npcMap)
            {
                if (kvp.Key >= newNpcId)
                {
                    kvp.Value.Id = newNpcId;
                    newNpcId++;

                    while (npcDB.npcs.Count < newNpcId)
                    {
                        npcDB.npcs.Add(null);
                    }
                    npcDB.npcs[kvp.Value.Id] = kvp.Value;

                    Logger.WriteLine("Newly added npc: " + kvp.Value.ToString());
                    hasChanges = true;
                    continue;
                }

                TriadNpc npcMatch = npcDB.Find(kvp.Value.Name);
                if (npcMatch == null)
                {
                    foreach (TriadNpc testNpc in npcDB.npcs)
                    {
                        if (testNpc != null &&
                            HasMatchingRewards(testNpc.Rewards, kvp.Value.Rewards) &&
                            HasMatchingRules(testNpc.Rules, kvp.Value.Rules) &&
                            testNpc.Deck.Equals(kvp.Value.Deck))
                        {
                            Logger.WriteLine("Npc name remap: " + testNpc.ToString() + " => " + kvp.Value.Name);
                            npcMatch = testNpc;
                            hasChanges = true;
                            break;
                        }
                    }
                }

                if (npcMatch != null)
                {
                    kvp.Value.Id = npcMatch.Id;
                    npcDB.npcs[kvp.Value.Id] = kvp.Value;
                }
                else
                {
                    Logger.WriteLine("Newly added npc: " + kvp.Value.ToString());

                    kvp.Value.Id = newNpcId;
                    newNpcId++;
                    hasChanges = true;

                    while (npcDB.npcs.Count < newNpcId)
                    {
                        npcDB.npcs.Add(null);
                    }
                    npcDB.npcs[kvp.Value.Id] = kvp.Value;
                }
            }

            if (hasChanges)
            {
                npcDB.Save();
            }

            return hasChanges;
        }

#endif // DEBUG
    }
}
