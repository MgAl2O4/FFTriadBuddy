﻿using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Xml;

namespace FFTriadBuddy
{
    public enum ELocStringType
    {
        Unknown,
        RuleName,
        CardType,
        CardName,
        NpcName,
        NpcLocation,
        TournamentName,
    }

    public class LocString
    {
        public string[] Text = new string[LocalizationDB.Languages.Length];
        public ELocStringType Type;
        public int Id;

        public LocString()
        {
            Type = ELocStringType.Unknown;
            Id = 0;
        }

        public LocString(ELocStringType Type, int Id)
        {
            this.Type = Type;
            this.Id = Id;
        }

        public LocString(ELocStringType Type, int Id, string DefaultText)
        {
            this.Type = Type;
            this.Id = Id;

            Text[LocalizationDB.CodeLanguageIdx] = DefaultText;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1} '{2}'", Type, Id, GetCodeName());
        }

        public string Get(string lang)
        {
            int langIdx = Array.IndexOf(LocalizationDB.Languages, lang);
            return Get(langIdx);
        }

        public string Get(int langIdx)
        {
            if (Text != null)
            {
                string resultStr = (langIdx < 0) ? null : Text[langIdx];
                if (string.IsNullOrEmpty(resultStr))
                {
                    // fallback language if game data is not available?
                    resultStr = Text[LocalizationDB.CodeLanguageIdx];
                }

                if (resultStr != null)
                {
                    return resultStr;
                }
            }

            return string.Format("--LOC:{0}:{1}--", Type, Id);
        }

        public string GetLocalized()
        {
            return Get(LocalizationDB.UserLanguageIdx);
        }

        public string GetCodeName()
        {
            return Get(LocalizationDB.CodeLanguageIdx);
        }
    }

    public class LocalizationDB
    {
        public readonly static string[] Languages = { "de", "en", "fr", "ja", "cn", "ko" };
        public readonly static string CodeLanguage = "en";
        public readonly static int CodeLanguageIdx = Array.IndexOf(Languages, CodeLanguage);
        public static int UserLanguageIdx = -1;
        public string DBPath;
        private static LocalizationDB instance = new LocalizationDB();

        public delegate void LangChangedDelegate();
        public static event LangChangedDelegate OnLanguageChanged;

        public List<LocString> LocUnknown;
        public List<LocString> LocRuleNames;
        public List<LocString> LocCardTypes;
        public List<LocString> LocCardNames;
        public List<LocString> LocNpcNames;
        public List<LocString> LocNpcLocations;
        public List<LocString> LocTournamentNames;

        public Dictionary<ELocStringType, List<LocString>> mapLocStrings;
        public Dictionary<ETriadCardType, LocString> mapCardTypes;

        public LocalizationDB()
        {
            DBPath = "data/loc.xml";

            LocUnknown = new List<LocString>();
            LocRuleNames = new List<LocString>();
            LocCardTypes = new List<LocString>();
            LocCardNames = new List<LocString>();
            LocNpcNames = new List<LocString>();
            LocNpcLocations = new List<LocString>();
            LocTournamentNames = new List<LocString>();

            mapLocStrings = new Dictionary<ELocStringType, List<LocString>>();
            mapLocStrings.Add(ELocStringType.Unknown, LocUnknown);
            mapLocStrings.Add(ELocStringType.RuleName, LocRuleNames);
            mapLocStrings.Add(ELocStringType.CardType, LocCardTypes);
            mapLocStrings.Add(ELocStringType.CardName, LocCardNames);
            mapLocStrings.Add(ELocStringType.NpcName, LocNpcNames);
            mapLocStrings.Add(ELocStringType.NpcLocation, LocNpcLocations);
            mapLocStrings.Add(ELocStringType.TournamentName, LocTournamentNames);

            mapCardTypes = new Dictionary<ETriadCardType, LocString>();
            string[] enumNames = Enum.GetNames(typeof(ETriadCardType));
            for (int enumIdx = 0; enumIdx < enumNames.Length; enumIdx++)
            {
                var locStr = new LocString(ELocStringType.CardType, enumIdx, enumIdx == 0 ? "" : enumNames[enumIdx]);
                mapCardTypes.Add((ETriadCardType)enumIdx, locStr);
                LocCardTypes.Add(locStr);
            }
        }

        public static LocalizationDB Get()
        {
            return instance;
        }

        public static void SetCurrentUserLanguage(string cultureCode)
        {
            int newLangIdx = CodeLanguageIdx;
            if (cultureCode == "de" || cultureCode.StartsWith("de-"))
            {
                newLangIdx = Array.IndexOf(Languages, "de");
            }
            else if (cultureCode == "fr" || cultureCode.StartsWith("fr-"))
            {
                newLangIdx = Array.IndexOf(Languages, "fr");
            }
            else if (cultureCode == "ja" || cultureCode.StartsWith("ja-"))
            {
                newLangIdx = Array.IndexOf(Languages, "ja");
            }
            else if (cultureCode == "ko" || cultureCode.StartsWith("ko-"))
            {
                newLangIdx = Array.IndexOf(Languages, "ko");
            }
            else if (cultureCode == "zh" || cultureCode.StartsWith("zh-"))
            {
                newLangIdx = Array.IndexOf(Languages, "cn");
            }
            else
            {
                newLangIdx = CodeLanguageIdx;
            }

            bool changed = false;
            if (newLangIdx != UserLanguageIdx)
            {
                UserLanguageIdx = newLangIdx;
                changed = true;
            }

            var locManager = LocResourceManager.Get();
            if (locManager.UserCultureCode != cultureCode)
            {
                CultureInfo.CurrentUICulture = new CultureInfo(cultureCode);
                locManager.SetCurrentUserLanguage(CultureInfo.CurrentUICulture, typeof(loc.strings));
                changed = true;
            }

            if (changed)
            {
                OnLanguageChanged?.Invoke();
                Logger.WriteLine("Init localization: culture:{0} -> gameData:{1}, UI:{2}", cultureCode, Languages[UserLanguageIdx], locManager.UserCultureCode);
            }
        }

        public LocString FindOrAddLocString(ELocStringType Type, int Id)
        {
            var list = mapLocStrings[Type];

            // so far those ids are continuous from 0, switch to dictionaries when it changes
            while (list.Count <= Id)
            {
                list.Add(new LocString(Type, list.Count));
            }

            return list[Id];
        }

        public bool Load()
        {
            int numLoaded = 0;

            try
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(AssetManager.Get().GetAsset(DBPath));

                foreach (XmlNode locNode in xdoc.DocumentElement.ChildNodes)
                {
                    XmlElement locElem = (XmlElement)locNode;
                    if (locElem != null && locElem.Name == "loc")
                    {
                        try
                        {
                            int locType = int.Parse(locElem.GetAttribute("type"));
                            int locId = int.Parse(locElem.GetAttribute("id"));

                            LocString locStr = FindOrAddLocString((ELocStringType)locType, locId);
                            for (int idx = 0; idx < locStr.Text.Length; idx++)
                            {
                                if (locElem.HasAttribute(Languages[idx]))
                                {
                                    locStr.Text[idx] = WebUtility.HtmlDecode(locElem.GetAttribute(Languages[idx]));
                                }
                            }

                            FixLocalizedNameCasing(locStr);
                            numLoaded++;
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

            Logger.WriteLine("Loaded localized strings: " + numLoaded);
            return numLoaded > 0;
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

                foreach (var kvp in mapLocStrings)
                {
                    foreach (var locStr in kvp.Value)
                    {
                        bool isEmpty = true;
                        for (int idx = 0; idx < locStr.Text.Length; idx++)
                        {
                            if (locStr.Text[idx] != null)
                            {
                                // empty str is valid value, ignore only nulls
                                isEmpty = false;
                                break;
                            }
                        }

                        if (!isEmpty)
                        {
                            xmlWriter.WriteStartElement("loc");
                            xmlWriter.WriteAttributeString("type", ((int)locStr.Type).ToString());
                            xmlWriter.WriteAttributeString("id", locStr.Id.ToString());

                            for (int idx = 0; idx < locStr.Text.Length; idx++)
                            {
                                if (locStr.Text[idx] != null)
                                {
                                    xmlWriter.WriteAttributeString(Languages[idx], locStr.Text[idx]);
                                }
                            }

                            xmlWriter.WriteEndElement();
                        }
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

        private static string[] caseFixExclusions = new string[] { "the", "goe", "van", "des", "sas", "yae", "tol", "der", "rem" };
        private void FixLocalizedNameCasing(LocString entry)
        {
            bool canFixCase = (entry.Type == ELocStringType.CardName) || (entry.Type == ELocStringType.NpcName);
            if (!canFixCase)
            {
                return;
            }

            for (int idxLang = 0; idxLang < entry.Text.Length; idxLang++)
            {
                if (string.IsNullOrEmpty(entry.Text[idxLang]))
                {
                    continue;
                }

                string[] tokens = entry.Text[idxLang].Split(' ');
                int numChangedTokens = 0;

                for (int idx = 0; idx < tokens.Length; idx++)
                {
                    if (tokens[idx].Length > 2 && Array.IndexOf(caseFixExclusions, tokens[idx]) < 0)
                    {
                        if (tokens[idx][1] == '\'')
                        {
                            // don't touch, i have no idea how french capitalization work
                            continue;
                        }

                        bool hasLowerCase = char.IsLower(tokens[idx], 0);
                        if (hasLowerCase)
                        {
                            var newToken = tokens[idx].Substring(0, 1).ToUpper() + tokens[idx].Substring(1);
                            tokens[idx] = newToken;
                            numChangedTokens++;
                        }
                    }
                }

                if (numChangedTokens > 0)
                {
                    var newText = string.Join(" ", tokens);
                    entry.Text[idxLang] = newText;
                }
            }
        }
    }
}
