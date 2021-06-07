using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;

#if DEBUG

namespace FFTriadBuddy.Datamine
{
    public class GameData
    {
        public int Id = 0;
        public virtual bool IsRawDataValid(CsvLocalizedData rawData) { return false; }
        public virtual void Parse(CsvLocalizedData rawData, int rowIdx) { }
        public virtual bool IsValid() { return Id > 0; }
        public virtual bool Link(GameDataLists lists) { return false; }
    }


    public class GameDataRule : GameData
    {
        public LocString Name;

        public override string ToString() { return Id + ": " + Name; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 8; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            if (Id > 0)
            {
                Name = rawData.GetLocalizedText(rowIdx, 1);
            }
        }

        public override bool Link(GameDataLists lists)
        {
            // no child nodes
            return true;
        }
    }

    public class GameDataCardType : GameData
    {
        public LocString Type;

        public override string ToString() { return Id + ": " + Type; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 2; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            if (Id > 0)
            {
                Type = rawData.GetLocalizedText(rowIdx, 1);
            }
        }

        public override bool Link(GameDataLists lists)
        {
            // no child nodes
            return true;
        }
    }

    public class GameDataCardName : GameData
    {
        public LocString Name;

        public override string ToString() { return Id + ": " + Name; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 10; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            if (Id > 0)
            {
                Name = rawData.GetLocalizedText(rowIdx, 1);
            }
        }

        public override bool Link(GameDataLists lists)
        {
            // no child nodes
            return true;
        }
    }

    public class GameDataCard : GameData
    {
        public int rarityIdx;
        public string cardType;
        public int sideTop;
        public int sideBottom;
        public int sideLeft;
        public int sideRight;
        public int sortOrder;
        public int uiGroup;

        public Dictionary<string, int[]> locSides = new Dictionary<string, int[]>();

        public GameDataCardType LinkedType;
        public GameDataCardName LinkedName;

        public override string ToString() { return string.Format("{0}: [{1}-{2}-{3}-{4}]", Id, sideTop, sideLeft, sideBottom, sideRight); }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 17; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            if (Id > 0)
            {
                sideTop = int.Parse(defRow[2]);
                sideBottom = int.Parse(defRow[3]);
                sideLeft = int.Parse(defRow[5]);
                sideRight = int.Parse(defRow[4]);

                cardType = defRow[7];

                int rarityStrLen = defRow[6].Length;
                char lastRarityChar = (rarityStrLen > 0) ? defRow[6][rarityStrLen - 1] : '0';
                rarityIdx = lastRarityChar - '0';

                sortOrder = int.Parse(defRow[10]);
                uiGroup = int.Parse(defRow[11]);

                foreach (var kvp in rawData.mapLanguages)
                {
                    if (kvp.Key != CsvLocalizedData.DefaultLanguage)
                    {
                        string[] locRow = (rowIdx < kvp.Value.rows.Count) ? kvp.Value.rows[rowIdx] : null;
                        if (locRow != null)
                        {
                            int[] sides = new int[] {
                                int.Parse(locRow[2]), // top
                                int.Parse(locRow[5]), // left
                                int.Parse(locRow[3]), // bottom
                                int.Parse(locRow[4])  // right
                            };

                            bool isValid = (sides[0] > 0) && (sides[1] > 0) && (sides[2] > 0) && (sides[3] > 0);
                            if (isValid)
                            {
                                locSides.Add(kvp.Key, sides);
                            }
                        }
                    }
                }
            }
        }

        public bool HasMatchingLocSide(int[] sideNums)
        {
            return (sideNums[0] == sideTop) && (sideNums[1] == sideLeft) && (sideNums[2] == sideBottom) && (sideNums[3] == sideRight);
        }

        public override bool Link(GameDataLists lists)
        {
            LinkedType = lists.cardTypes.Find(x => (x.Type.GetCodeName() == cardType));
            // null is allowed LinkType value

            LinkedName = lists.cardNames.Find(x => (x.Id == Id));
            if (LinkedName == null)
            {
                Logger.WriteLine("FAILED link: GameDataCard, Id:{1}, no matching Name", Id);
                return false;
            }

            return true;
        }
    }

    public class GameDataNpcTriadId : GameData
    {
        public static readonly string TriadIdPattern = "TripleTriad#";

        public int TriadId;

        public GameDataNpcLocation LinkedLocation;
        public GameDataNpcName LinkedName;
        public GameDataNpc LinkedNpc;

        public override string ToString() { return Id + ": " + TriadId; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() > 8; }
        public override bool IsValid() { return TriadId != 0; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            TriadId = 0;

            for (int colIdx = 3; colIdx < defRow.Length; colIdx++)
            {
                if (defRow[colIdx].StartsWith(TriadIdPattern))
                {
                    TriadId = int.Parse(defRow[colIdx].Substring(TriadIdPattern.Length));
                    break;
                }
            }
        }

        public override bool Link(GameDataLists lists)
        {
            LinkedNpc = lists.npcs.Find(x => (x.Id == TriadId));
            if (LinkedNpc == null)
            {
                Logger.WriteLine("FAILED link: GameDataNpcTriadId, NpcBaseId:{0}, TriadId:{1}, no matching NPC", Id, TriadId);
                return false;
            }

            LinkedLocation = lists.npcLocations.Find(x => (x.Id == Id));
            if (LinkedLocation == null)
            {
                Logger.WriteLine("Warning: GameDataNpcTriadId, NpcBaseId:{0}, no matching Location", Id);
                //Logger.WriteLine("FAILED link: GameDataNpcTriadId, NpcBaseId:{0}, no matching Location", Id);
                //return false;
            }
            else
            {
                LinkedLocation.LinkedOwner = this;
            }

            LinkedName = lists.npcNames.Find(x => (x.Id == Id));
            if (LinkedName == null)
            {
                Logger.WriteLine("FAILED link: GameDataNpcTriadId, NpcBaseId:{0}, no matching Name", Id);
                return false;
            }

            if (LinkedName.Name.GetCodeName().StartsWith("Wyra"))
            {
                int a = 1;
                a++;
            }

            return true;
        }
    }

    public class GameDataNpcName : GameData
    {
        public LocString Name;

        public override string ToString() { return Id + ": " + Name; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 12; }
        public override bool IsValid() { return Name != null; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            if (defRow[1].Length > 0)
            {
                Name = rawData.GetLocalizedText(rowIdx, 1);
            }
        }

        public override bool Link(GameDataLists lists)
        {
            // no child nodes
            return true;
        }
    }

    public class GameDataNpcLocation : GameData
    {
        public static readonly string NpcId = "ENpcBase#";
        public static readonly string NpcType = "8";

        public string MapCode;
        public float ScaledPosX;
        public float ScaledPosZ;

        public GameDataMap LinkedMap;
        public GameDataNpcTriadId LinkedOwner;

        public override string ToString() { return Id + ": " + MapCode; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 11; }
        public override bool IsValid() { return !string.IsNullOrEmpty(MapCode); }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            if (defRow[6] == NpcType && defRow[7].StartsWith(NpcId))
            {
                Id = int.Parse(defRow[7].Substring(NpcId.Length));
                ScaledPosX = float.Parse(defRow[1], CultureInfo.InvariantCulture);
                ScaledPosZ = float.Parse(defRow[3], CultureInfo.InvariantCulture);
                MapCode = defRow[8];
            }
        }

        public override bool Link(GameDataLists lists)
        {
            if (LinkedOwner != null && LinkedOwner.LinkedNpc != null)
            {
                LinkedMap = lists.maps.Find(x => (x.MapCode == MapCode));
                if (LinkedMap == null)
                {
                    Logger.WriteLine("FAILED link: GameDataNpcLocation, NpcBaseId:{0}, MapCode:{1}, no matching Map", Id, MapCode);
                    return false;
                }
            }

            return true;
        }
    }

    public class GameDataNpc : GameData
    {
        public List<string> CardsFixed;
        public List<string> CardsVariable;
        public List<string> Rules;
        public List<string> Rewards;

        public Dictionary<string, string[]> locCards = new Dictionary<string, string[]>();

        public GameDataNpcTriadId LinkedNpcId;
        public List<GameDataCard> LinkedCardsFixed;
        public List<GameDataCard> LinkedCardsVariable;
        public List<GameDataRule> LinkedRules;
        public List<GameDataReward> LinkedRewards;

        public override string ToString() { return string.Format("{0}: {1}", Id.ToString(), LinkedNpcId != null && LinkedNpcId.LinkedName != null ? LinkedNpcId.LinkedName.Name.GetCodeName() : ""); }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 31; }
        public override bool IsValid() { return (CardsFixed.Count > 0) || (CardsVariable.Count > 0); }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            if (Id > 0)
            {
                CardsFixed = new List<string>();
                for (int idx = 1; idx <= 5; idx++)
                {
                    if (defRow[idx].Length > 0 && defRow[idx] != "0")
                    {
                        CardsFixed.Add(defRow[idx]);
                    }
                }

                CardsVariable = new List<string>();
                for (int idx = 6; idx <= 10; idx++)
                {
                    if (defRow[idx].Length > 0 && defRow[idx] != "0")
                    {
                        CardsVariable.Add(defRow[idx]);
                    }
                }

                Rules = new List<string>();
                for (int idx = 11; idx <= 12; idx++)
                {
                    if (defRow[idx].Length > 0)
                    {
                        Rules.Add(defRow[idx]);
                    }
                }

                Rewards = new List<string>();
                for (int idx = 27; idx <= 30; idx++)
                {
                    if (defRow[idx].Length > 0)
                    {
                        Rewards.Add(defRow[idx]);
                    }
                }

                foreach (var kvp in rawData.mapLanguages)
                {
                    if (kvp.Key != CsvLocalizedData.DefaultLanguage)
                    {
                        string[] locRow = (rowIdx < kvp.Value.rows.Count) ? kvp.Value.rows[rowIdx] : null;
                        if (locRow != null)
                        {
                            List<string> cardNames = new List<string>();
                            for (int idx = 1; idx <= 10; idx++)
                            {
                                if (locRow[idx].Length > 0 && locRow[idx] != "0")
                                {
                                    cardNames.Add(locRow[idx]);
                                }
                            }

                            if (cardNames.Count > 0)
                            {
                                locCards.Add(kvp.Key, cardNames.ToArray());
                            }
                        }
                    }
                }
            }
        }

        public bool HasMatchingCards(string[] cards)
        {
            if (CardsFixed.Count + CardsVariable.Count != cards.Length)
            {
                return false;
            }

            int testIdx = 0;
            for (int srcIdx = 0; srcIdx < CardsFixed.Count; srcIdx++, testIdx++)
            {
                if (CardsFixed[srcIdx] != cards[testIdx])
                {
                    return false;
                }
            }

            for (int srcIdx = 0; srcIdx < CardsVariable.Count; srcIdx++, testIdx++)
            {
                if (CardsVariable[srcIdx] != cards[testIdx])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Link(GameDataLists lists)
        {
            LinkedNpcId = lists.npcTriadIds.Find(x => (x.TriadId == Id));
            if (LinkedNpcId == null)
            {
                // removed NPC? ignore and fail this entire entry
                Logger.WriteLine("Warning: GameDataNpc, TriadId:{0}, no matching NPC", Id);
                return true;
            }

            LinkedCardsFixed = new List<GameDataCard>();
            foreach (var cardName in CardsFixed)
            {
                var LinkedCard = lists.cards.Find(x => (x.LinkedName.Name.GetCodeName() == cardName));
                if (LinkedCard == null)
                {
                    Logger.WriteLine("FAILED link: GameDataNpc, TriadId:{0}, Card:{1}, no matching card name", Id, cardName);
                    return false;
                }

                LinkedCardsFixed.Add(LinkedCard);
            }

            LinkedCardsVariable = new List<GameDataCard>();
            foreach (var cardName in CardsVariable)
            {
                var LinkedCard = lists.cards.Find(x => (x.LinkedName.Name.GetCodeName() == cardName));
                if (LinkedCard == null)
                {
                    Logger.WriteLine("FAILED link: GameDataNpc, TriadId:{0}, Card:{1}, no matching card name", Id, cardName);
                    return false;
                }

                LinkedCardsVariable.Add(LinkedCard);
            }

            LinkedRules = new List<GameDataRule>();
            foreach (var ruleName in Rules)
            {
                var LinkedRule = lists.rules.Find(x => (x.Name.GetCodeName() == ruleName));
                if (LinkedRule == null)
                {
                    Logger.WriteLine("FAILED link: GameDataNpc, TriadId:{0}, Rule:{1}, no matching rule name", Id, ruleName);
                    return false;
                }

                LinkedRules.Add(LinkedRule);
            }

            LinkedRewards = new List<GameDataReward>();
            foreach (var itemName in Rewards)
            {
                var LinkedReward = lists.rewards.Find(x => (x.CardName == itemName));
                if (LinkedReward == null && itemName.EndsWith(" Card"))
                {
                    var itemName2 = itemName.Substring(0, itemName.Length - 5);

                    LinkedReward = lists.rewards.Find(x => (x.CardName == itemName2));
                    if (LinkedReward == null)
                    {
                        var itemName3 = "The " + itemName2;
                        LinkedReward = lists.rewards.Find(x => (x.CardName == itemName3));
                    }
                }

                if (LinkedReward == null)
                {
                    Logger.WriteLine("FAILED link: GameDataNpc, TriadId:{0}, Reward:{1}, no matching item name", Id, itemName);
                    return false;
                }

                LinkedRewards.Add(LinkedReward);
            }

            return true;
        }
    }

    public class GameDataReward : GameData
    {
        public static readonly string CardItemType = "Triple Triad Card";

        public string CardName;

        public GameDataCard LinkedCard;

        public override string ToString() { return Id + ": " + CardName; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() >= 90; }
        public override bool IsValid() { return CardName != null; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            if (defRow[16] == CardItemType)
            {
                Id = int.Parse(defRow[0]);
                CardName = defRow[15];
            }
        }

        public override bool Link(GameDataLists lists)
        {
            LinkedCard = lists.cards.Find(x => (x.LinkedName.Name.GetCodeName() == CardName));
            if (LinkedCard == null)
            {
                Logger.WriteLine("FAILED link: GameDataReward, Id:{0}, Card:{1}, no matching card name", Id, CardName);
                return false;
            }

            return true;
        }
    }

    public class GameDataMap : GameData
    {
        public string Name;
        public string MapCode;
        public int Scale;
        public int OffsetX;
        public int OffsetY;

        public GameDataPlaceName LinkedPlace;

        public void GetCoords(float ScaledX, float ScaledY, out int MapX, out int MapY)
        {
            MapX = CovertCoordTo2d(ScaledX, OffsetX);
            MapY = CovertCoordTo2d(ScaledY, OffsetY);
        }

        private int CovertCoordTo2d(float Coord, float Offset)
        {
            float useScale = Scale / 100.0f;
            float useValue = (Coord + Offset) * useScale;
            return (int)Math.Round(((41.0 / useScale) * ((useValue + 1024.0) / 2048.0)) + 1);
        }

        public override string ToString() { return MapCode + ": " + Name; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 19; }
        public override bool IsValid() { return !string.IsNullOrEmpty(Name); }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            MapCode = defRow[7];
            if (MapCode.Length > 0 && !MapCode.StartsWith("default"))
            {
                Name = defRow[12];
                Scale = int.Parse(defRow[8]);
                OffsetX = int.Parse(defRow[9]);
                OffsetY = int.Parse(defRow[10]);
            }
        }

        public override bool Link(GameDataLists lists)
        {
            LinkedPlace = lists.placeNames.Find(x => (x.Name.GetCodeName() == Name));
            if (LinkedPlace == null)
            {
                Logger.WriteLine("FAILED link: GameDataMap, MapCode:{0}, Name:{1}, no matching place name", MapCode, Name);
                return false;
            }

            return true;
        }
    }

    public class GameDataPlaceName : GameData
    {
        public LocString Name;

        public override string ToString() { return Name.ToString(); }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 12; }
        public override bool IsValid() { return Name != null; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            if (defRow[1].Length > 0)
            {
                Name = rawData.GetLocalizedText(rowIdx, 1);
            }
        }

        public override bool Link(GameDataLists lists)
        {
            // no child nodes
            return true;
        }
    }

    public class GameDataTournamentName : GameData
    {
        public LocString Name;

        public override string ToString() { return Id + ": " + Name; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 2; }
        public override bool IsValid() { return Name != null; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            if (defRow[1].Length > 0)
            {
                Name = rawData.GetLocalizedText(rowIdx, 1);
            }
        }

        public override bool Link(GameDataLists lists)
        {
            // no child nodes
            return true;
        }
    }

    public class GameDataTournament : GameData
    {
        public List<int> RuleIds;

        public List<GameDataRule> LinkedRules;

        public override string ToString() { return Id + ": " + RuleIds.Count; }
        public override bool IsRawDataValid(CsvLocalizedData rawData) { return rawData.GetNumColumns() == 5; }

        public override void Parse(CsvLocalizedData rawData, int rowIdx)
        {
            string[] defRow = rawData.data.rows[rowIdx];

            Id = int.Parse(defRow[0]);
            if (Id > 0)
            {
                RuleIds = new List<int>();
                for (int idx = 1; idx <= 4; idx++)
                {
                    if (defRow[idx] != "0")
                    {
                        RuleIds.Add(int.Parse(defRow[idx]));
                    }
                }
            }
        }

        public override bool Link(GameDataLists lists)
        {
            LinkedRules = new List<GameDataRule>();
            foreach (var ruleId in RuleIds)
            {
                var LinkedRule = lists.rules.Find(x => (x.Id == ruleId));
                if (LinkedRule == null)
                {
                    Logger.WriteLine("FAILED link: GameDataTournament, Id:{0}, ruleId:{1}, no matching rule Id", Id, ruleId);
                    return false;
                }

                LinkedRules.Add(LinkedRule);
            }

            return true;
        }
    }

    public class GameDataLists
    {
        public List<GameDataRule> rules;
        public List<GameDataCardType> cardTypes;
        public List<GameDataCardName> cardNames;
        public List<GameDataCard> cards;
        public List<GameDataNpcTriadId> npcTriadIds;
        public List<GameDataNpcName> npcNames;
        public List<GameDataNpcLocation> npcLocations;
        public List<GameDataNpc> npcs;
        public List<GameDataReward> rewards;
        public List<GameDataMap> maps;
        public List<GameDataPlaceName> placeNames;
        public List<GameDataTournamentName> tournamentNames;
        public List<GameDataTournament> tournaments;

        public void Load(string folderPath)
        {
            Logger.WriteLine("Loading csv files...");
            rules = LoadGameData<GameDataRule>(folderPath, "TripleTriadRule");
            cardTypes = LoadGameData<GameDataCardType>(folderPath, "TripleTriadCardType");
            cardNames = LoadGameData<GameDataCardName>(folderPath, "TripleTriadCard");
            cards = LoadGameData<GameDataCard>(folderPath, "TripleTriadCardResident");

            npcTriadIds = LoadGameData<GameDataNpcTriadId>(folderPath, "ENpcBase");
            npcNames = LoadGameData<GameDataNpcName>(folderPath, "ENpcResident");
            npcLocations = LoadGameData<GameDataNpcLocation>(folderPath, "Level");
            npcs = LoadGameData<GameDataNpc>(folderPath, "TripleTriad");
            rewards = LoadGameData<GameDataReward>(folderPath, "Item");
            maps = LoadGameData<GameDataMap>(folderPath, "Map");
            placeNames = LoadGameData<GameDataPlaceName>(folderPath, "PlaceName");

            tournamentNames = LoadGameData<GameDataTournamentName>(folderPath, "TripleTriadCompetition");
            tournaments = LoadGameData<GameDataTournament>(folderPath, "TripleTriadTournament");
        }

        public bool Link()
        {
            Logger.WriteLine("Linking database entries...");

            bool result = true;
            result = result && LinkGameData(rules);
            result = result && LinkGameData(cardTypes);
            result = result && LinkGameData(cardNames);
            result = result && LinkGameData(cards);

            result = result && LinkGameData(npcTriadIds);
            result = result && LinkGameData(npcNames);
            result = result && LinkGameData(npcs);
            result = result && LinkGameData(rewards);
            result = result && LinkGameData(npcLocations); // must be after npcs
            result = result && LinkGameData(maps);
            result = result && LinkGameData(placeNames);

            result = result && LinkGameData(tournamentNames);
            result = result && LinkGameData(tournaments);

            return result;
        }

        private List<T> LoadGameData<T>(string folderPath, string pathNoExt) where T : GameData, new()
        {
            var rawData = CsvLocalizedData.LoadFrom(folderPath + pathNoExt + ".csv");
            int numRows = rawData.GetNumRows();

            if (numRows == 0)
            {
                Logger.WriteLine("FAILED to load {0}: empty", pathNoExt);
                return null;
            }

            T testOb = new T();
            if (!testOb.IsRawDataValid(rawData))
            {
                Logger.WriteLine("FAILED to load {0}: layout mismatch!", pathNoExt);
                return null;
            }

            var resultList = new List<T>();
            for (int idx = 0; idx < numRows; idx++)
            {
                T entryOb = new T();
                entryOb.Parse(rawData, idx);

                if (entryOb.IsValid())
                {
                    resultList.Add(entryOb);
                }
            }

            Logger.WriteLine(">> loaded {0}, entries: {1}", pathNoExt, resultList.Count);
            return resultList;
        }

        private bool LinkGameData<T>(List<T> list) where T : GameData
        {
            foreach (var entry in list)
            {
                if (!entry.Link(this))
                {
                    Logger.WriteLine("FAILED to link!");
                    return false;
                }
            }

            return true;
        }
    }
}

#endif // DEBUG
