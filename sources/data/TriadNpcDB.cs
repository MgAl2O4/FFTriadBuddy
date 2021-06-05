using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Xml;

namespace FFTriadBuddy
{
    public class TriadNpc
    {
        public int Id;
        public LocString Name;
        public LocString LocationMap;
        public int LocationX;
        public int LocationY;
        public List<TriadGameModifier> Rules;
        public List<TriadCard> Rewards;
        public TriadDeck Deck;

        public TriadNpc(int id, List<TriadGameModifier> rules, List<TriadCard> rewards, int[] cardsAlways, int[] cardsPool)
        {
            Id = id;
            Name = LocalizationDB.Get().FindOrAddLocString(ELocStringType.NpcName, id);
            LocationMap = LocalizationDB.Get().FindOrAddLocString(ELocStringType.NpcLocation, id);
            Rules = rules;
            Rewards = rewards;
            Deck = new TriadDeck(cardsAlways, cardsPool);
        }

        public TriadNpc(int id, List<TriadGameModifier> rules, List<TriadCard> rewards, TriadDeck deck)
        {
            Id = id;
            Name = LocalizationDB.Get().FindOrAddLocString(ELocStringType.NpcName, id);
            LocationMap = LocalizationDB.Get().FindOrAddLocString(ELocStringType.NpcLocation, id);
            Rules = rules;
            Rewards = rewards;
            Deck = deck;
        }

        public override string ToString()
        {
            return Name.GetCodeName();
        }

        public string GetLocationDesc()
        {
            return string.Format("{0} ({1}, {2})", LocationMap.GetLocalized(), LocationX, LocationY);
        }
    }

    public class TriadNpcDB
    {
        public List<TriadNpc> npcs;
        public string DBPath;
        private static TriadNpcDB instance = new TriadNpcDB();

        public TriadNpcDB()
        {
            DBPath = "data/npcs.xml";
            npcs = new List<TriadNpc>();
        }

        public static TriadNpcDB Get()
        {
            return instance;
        }

        public bool Load()
        {
            try
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(AssetManager.Get().GetAsset(DBPath));

                foreach (XmlNode npcNode in xdoc.DocumentElement.ChildNodes)
                {
                    XmlElement npcElem = (XmlElement)npcNode;
                    if (npcElem != null && npcElem.Name == "npc")
                    {
                        try
                        {
                            List<TriadGameModifier> rules = new List<TriadGameModifier>();
                            List<TriadCard> rewards = new List<TriadCard>();
                            int[] deckA = new int[5];
                            int[] deckV = new int[5];

                            foreach (XmlNode innerNode in npcElem.ChildNodes)
                            {
                                XmlElement testElem = (XmlElement)innerNode;
                                if (testElem != null)
                                {
                                    if (testElem.Name == "rule")
                                    {
                                        int ruleId = int.Parse(testElem.GetAttribute("id"));
                                        rules.Add(TriadGameModifierDB.Get().mods[ruleId].Clone());
                                    }
                                    else if (testElem.Name == "reward")
                                    {
                                        int cardId = int.Parse(testElem.GetAttribute("id"));
                                        rewards.Add(TriadCardDB.Get().cards[cardId]);
                                    }
                                    else if (testElem.Name == "deckA")
                                    {
                                        deckA[0] = int.Parse(testElem.GetAttribute("id0"));
                                        deckA[1] = int.Parse(testElem.GetAttribute("id1"));
                                        deckA[2] = int.Parse(testElem.GetAttribute("id2"));
                                        deckA[3] = int.Parse(testElem.GetAttribute("id3"));
                                        deckA[4] = int.Parse(testElem.GetAttribute("id4"));
                                    }
                                    else if (testElem.Name == "deckV")
                                    {
                                        deckV[0] = int.Parse(testElem.GetAttribute("id0"));
                                        deckV[1] = int.Parse(testElem.GetAttribute("id1"));
                                        deckV[2] = int.Parse(testElem.GetAttribute("id2"));
                                        deckV[3] = int.Parse(testElem.GetAttribute("id3"));
                                        deckV[4] = int.Parse(testElem.GetAttribute("id4"));
                                    }
                                }
                            }

                            TriadNpc newNpc = new TriadNpc(
                                int.Parse(npcElem.GetAttribute("id")),
                                rules,
                                rewards,
                                deckA,
                                deckV);
                            newNpc.LocationX = int.Parse(npcElem.GetAttribute("mx"));
                            newNpc.LocationY = int.Parse(npcElem.GetAttribute("my"));

                            while (npcs.Count <= newNpc.Id)
                            {
                                npcs.Add(null);
                            }

                            npcs[newNpc.Id] = newNpc;
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

            Logger.WriteLine("Loaded npcs: " + npcs.Count);
            return npcs.Count > 0;
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

                foreach (TriadNpc npc in npcs)
                {
                    if (npc != null)
                    {
                        xmlWriter.WriteStartElement("npc");
                        xmlWriter.WriteAttributeString("id", npc.Id.ToString());
                        xmlWriter.WriteAttributeString("mx", npc.LocationX.ToString());
                        xmlWriter.WriteAttributeString("my", npc.LocationY.ToString());

                        xmlWriter.WriteStartElement("deckA");
                        for (int Idx = 0; Idx < 5; Idx++)
                        {
                            xmlWriter.WriteAttributeString("id" + Idx, (npc.Deck.knownCards != null && npc.Deck.knownCards.Count > Idx) ? npc.Deck.knownCards[Idx].Id.ToString() : "0");
                        }
                        xmlWriter.WriteEndElement();

                        xmlWriter.WriteStartElement("deckV");
                        for (int Idx = 0; Idx < 5; Idx++)
                        {
                            xmlWriter.WriteAttributeString("id" + Idx, (npc.Deck.unknownCardPool != null && npc.Deck.unknownCardPool.Count > Idx) ? npc.Deck.unknownCardPool[Idx].Id.ToString() : "0");
                        }
                        xmlWriter.WriteEndElement();

                        for (int Idx = 0; Idx < npc.Rules.Count; Idx++)
                        {
                            xmlWriter.WriteStartElement("rule");
                            xmlWriter.WriteAttributeString("id", npc.Rules[Idx].GetLocalizationId().ToString());
                            xmlWriter.WriteEndElement();
                        }

                        for (int Idx = 0; Idx < npc.Rewards.Count; Idx++)
                        {
                            xmlWriter.WriteStartElement("reward");
                            xmlWriter.WriteAttributeString("id", npc.Rewards[Idx].Id.ToString());
                            xmlWriter.WriteEndElement();
                        }

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

        public TriadNpc Find(string Name)
        {
            foreach (TriadNpc testNpc in npcs)
            {
                if (testNpc != null && testNpc.Name.GetCodeName().Equals(Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return testNpc;
                }
            }

            return null;
        }

        public List<TriadNpc> FindByReward(TriadCard card)
        {
            List<TriadNpc> result = new List<TriadNpc>();
            foreach (TriadNpc testNpc in npcs)
            {
                if (testNpc != null && testNpc.Rewards.Contains(card))
                {
                    result.Add(testNpc);
                }
            }

            return result;
        }

        public TriadNpc FindByDeckId(string deckId)
        {
            foreach (TriadNpc testNpc in npcs)
            {
                if (testNpc != null && testNpc.Deck != null && testNpc.Deck.deckId == deckId)
                {
                    return testNpc;
                }
            }

            return null;
        }
    }
}
