using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Xml;

namespace FFTriadBuddy
{
    public class TriadTournament
    {
        public readonly string Name;
        public readonly List<TriadGameModifier> Rules;

        public TriadTournament(string name, List<TriadGameModifier> rules)
        {
            Name = name;
            Rules = rules;
        }

        public override string ToString()
        {
            return Name;
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
            List<TriadTournament> loadedTypes = new List<TriadTournament>();

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
                                        rules.Add(ParseRule(testElem.GetAttribute("name"), modObjects));
                                    }
                                }
                            }

                            TriadTournament newTournament = new TriadTournament(
                                WebUtility.HtmlDecode(ttElem.GetAttribute("name")),
                                rules);

                            loadedTypes.Add(newTournament);
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

            if (loadedTypes.Count > 0)
            {
                tournaments.Clear();
                tournaments.AddRange(loadedTypes);
            }

            Logger.WriteLine("Loaded tournaments: " + tournaments.Count);
            return tournaments.Count > 0;
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
    }
}
