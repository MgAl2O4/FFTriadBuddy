using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FFTriadBuddy
{
    public class TriadNpc
    {
        public int Id;
        public string Name;
        public string Location;
        public List<TriadGameModifier> Rules;
        public List<TriadCard> Rewards;
        public TriadDeck Deck;

        public TriadNpc(int id, string name, string location,
            List<TriadGameModifier> rules, List<TriadCard> rewards, int[] cardsAlways, int[] cardsPool)
        {
            Id = id;
            Name = name;
            Location = location;
            Rules = rules;
            Rewards = rewards;
            Deck = new TriadDeck(cardsAlways, cardsPool);
        }

        public override string ToString()
        {
            return Name;
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
            List<TriadNpc> loadedNpcs = new List<TriadNpc>();
            int maxLoadedId = 0;

            List<TriadGameModifier> modObjects = new List<TriadGameModifier>();
            foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
            {
                if (type.IsSubclassOf(typeof(TriadGameModifier)))
                {
                    modObjects.Add((TriadGameModifier)Activator.CreateInstance(type));
                }
            }

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
                                        rules.Add(ParseRule(testElem.GetAttribute("name"), modObjects));
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
                                WebUtility.HtmlDecode(npcElem.GetAttribute("name")),
                                WebUtility.HtmlDecode(npcElem.GetAttribute("location")),
                                rules,
                                rewards,
                                deckA,
                                deckV);

                            loadedNpcs.Add(newNpc);
                            maxLoadedId = Math.Max(maxLoadedId, newNpc.Id);
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

            if (loadedNpcs.Count > 0)
            {
                while (npcs.Count < (maxLoadedId + 1))
                {
                    npcs.Add(null);
                }

                foreach (TriadNpc npc in loadedNpcs)
                {
                    npcs[npc.Id] = npc;
                }
            }

            Logger.WriteLine("Loaded npcs: " + npcs.Count);
            return loadedNpcs.Count > 0;
        }

        public void Save()
        {
            string RawFilePath = AssetManager.Get().CreateFilePath("assets/" + DBPath) + ".new";
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
                        xmlWriter.WriteAttributeString("name", npc.Name);
                        xmlWriter.WriteAttributeString("location", npc.Location);

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
                            xmlWriter.WriteAttributeString("name", npc.Rules[Idx].GetName());
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

        private TriadGameModifier ParseRule(string ruleName, List<TriadGameModifier> ruleTypes)
        {
            TriadGameModifier result = null;
            foreach (TriadGameModifier mod in ruleTypes)
            {
                if (ruleName.Equals(mod.GetName(), StringComparison.InvariantCultureIgnoreCase))
                {
                    result = (TriadGameModifier)Activator.CreateInstance(mod.GetType());
                    break;
                }
            }

            if (result == null)
            {
                Logger.WriteLine("Loading failed! Can't parse rule: " + ruleName);
            }

            return result;
        }

        public TriadNpc Find(string Name)
        {
            foreach (TriadNpc testNpc in npcs)
            {
                if (testNpc != null && 
                    testNpc.Name.Equals(Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return testNpc;
                }
            }

            return null;
        }
    }
}
