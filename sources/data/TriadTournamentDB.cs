using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Xml;

namespace FFTriadBuddy
{
    public class TriadTournament
    {
        public readonly LocString Name;
        public readonly List<TriadGameModifier> Rules;
        public readonly int Id;

        public TriadTournament(int id, List<TriadGameModifier> rules)
        {
            Id = id;
            Name = LocalizationDB.Get().FindOrAddLocString(ELocStringType.TournamentName, id);
            Rules = rules;
        }

        public override string ToString()
        {
            return Name.GetCodeName();
        }
    }

    public class TriadTournamentDB
    {
        public List<TriadTournament> tournaments;
        public string DBPath;
        private static TriadTournamentDB instance = new TriadTournamentDB();

        public TriadTournamentDB()
        {
            DBPath = "data/tournaments.xml";
            tournaments = new List<TriadTournament>();
        }

        public static TriadTournamentDB Get()
        {
            return instance;
        }

        public bool Load()
        {
            try
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(AssetManager.Get().GetAsset(DBPath));

                foreach (XmlNode ttNode in xdoc.DocumentElement.ChildNodes)
                {
                    XmlElement ttElem = (XmlElement)ttNode;
                    if (ttElem != null && ttElem.Name == "tournament")
                    {
                        try
                        {
                            List<TriadGameModifier> rules = new List<TriadGameModifier>();
                            foreach (XmlNode innerNode in ttElem.ChildNodes)
                            {
                                XmlElement testElem = (XmlElement)innerNode;
                                if (testElem != null)
                                {
                                    if (testElem.Name == "rule")
                                    {
                                        int ruleId = int.Parse(testElem.GetAttribute("id"));
                                        rules.Add(TriadGameModifierDB.Get().mods[ruleId].Clone());
                                    }
                                }
                            }

                            TriadTournament newTournament = new TriadTournament(int.Parse(ttElem.GetAttribute("id")), rules);
                            while (tournaments.Count <= newTournament.Id)
                            {
                                tournaments.Add(null);
                            }
                            tournaments[newTournament.Id] = newTournament;
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

            Logger.WriteLine("Loaded tournaments: " + tournaments.Count);
            return tournaments.Count > 0;
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

                foreach (TriadTournament tournament in tournaments)
                {
                    xmlWriter.WriteStartElement("tournament");
                    xmlWriter.WriteAttributeString("id", tournament.Id.ToString());

                    for (int Idx = 0; Idx < tournament.Rules.Count; Idx++)
                    {
                        xmlWriter.WriteStartElement("rule");
                        xmlWriter.WriteAttributeString("id", tournament.Rules[Idx].GetLocalizationId().ToString());
                        xmlWriter.WriteEndElement();
                    }

                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndDocument();
                xmlWriter.Close();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Saving failed! Exception:" + ex);
            }
        }

    }
}
