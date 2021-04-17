using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace FFTriadBuddy
{
    public class ScreenshotVerify
    {
#if DEBUG
        private static Dictionary<string, TriadGameModifier> mapRules;

        public static void RunAutoVerify()
        {
            mapRules = new Dictionary<string, TriadGameModifier>();
            foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
            {
                if (type.IsSubclassOf(typeof(TriadGameModifier)))
                {
                    TriadGameModifier modInstance = (TriadGameModifier)Activator.CreateInstance(type);
                    mapRules.Add(modInstance.GetName(), modInstance);
                }
            }

            string testRoot = AssetManager.Get().CreateFilePath("test/auto");
            IEnumerable<string> configPaths = Directory.EnumerateFiles(testRoot, "*.json");
            foreach (var configPath in configPaths)
            {
                string imagePath = configPath.Replace(".json", ".jpg");
                if (File.Exists(imagePath))
                {
                    bool bNeedsDebugRun = false;

                    try
                    {
                        RunVerify(configPath, imagePath, false);
                    }
                    catch (Exception)
                    {
                        bNeedsDebugRun = true;
                    }

                    // retry, don't catch exceptions
                    if (bNeedsDebugRun)
                    {
                        RunVerify(configPath, imagePath, true);
                    }
                }
            }
        }

        private enum ECardState
        {
            None,
            Hidden,
            Locked,
            Visible,
            PlacedRed,
            PlacedBlue,
        }

        private class VerifyCard
        {
            public ECardState state;
            public int[] sides;
            public int mod;

            public VerifyCard()
            {
                state = ECardState.None;
                sides = new int[4] { 0, 0, 0, 0 };
                mod = 0;
            }

            public override string ToString()
            {
                return string.Format("{0} [{1},{2},{3},{4}] {5}",
                    state, sides[0], sides[1], sides[2], sides[3],
                    mod == 0 ? "" : (mod > 0 ? ("+" + mod) : mod.ToString()));
            }
        }

        private class VerifyConfig
        {
            public string[] rules;
            public VerifyCard[] deckBlue;
            public VerifyCard[] deckRed;
            public VerifyCard[] board;

            public VerifyConfig()
            {
                rules = null;
                deckBlue = new VerifyCard[5];
                deckRed = new VerifyCard[5];
                board = new VerifyCard[9];
            }

            public override string ToString()
            {
                return (rules == null) ? "No rules" : string.Join(", ", rules);
            }
        }

        private static ECardState ParseCardState(string stateStr)
        {
            if (stateStr == "empty") return ECardState.None;
            if (stateStr == "hidden") return ECardState.Hidden;
            if (stateStr == "locked") return ECardState.Locked;
            if (stateStr == "visible") return ECardState.Visible;
            if (stateStr == "red") return ECardState.PlacedRed;
            if (stateStr == "blue") return ECardState.PlacedBlue;

            return ECardState.None;
        }

        private static VerifyCard ParseConfigCardData(JsonParser.ObjectValue cardOb)
        {
            VerifyCard cardData = new VerifyCard();
            cardData.state = ParseCardState(cardOb["state"] as JsonParser.StringValue);

            if (cardOb.entries.ContainsKey("sides"))
            {
                JsonParser.ArrayValue sidesArr = cardOb.entries["sides"] as JsonParser.ArrayValue;
                for (int idx = 0; idx < 4; idx++)
                {
                    cardData.sides[idx] = sidesArr.entries[idx] as JsonParser.IntValue;
                }
            }

            if (cardOb.entries.ContainsKey("mod"))
            {
                cardData.mod = cardOb["mod"] as JsonParser.IntValue;
            }

            return cardData;
        }

        private static VerifyConfig LoadConfigData(string configPath)
        {
            VerifyConfig configData = new VerifyConfig();
            string configText = File.ReadAllText(configPath);

            JsonParser.ObjectValue rootOb = JsonParser.ParseJson(configText);

            JsonParser.ArrayValue ruleArr = rootOb.entries["rules"] as JsonParser.ArrayValue;
            configData.rules = new string[ruleArr.entries.Count];
            for (int idx = 0; idx < ruleArr.entries.Count; idx++)
            {
                configData.rules[idx] = ruleArr.entries[idx] as JsonParser.StringValue;
            }

            JsonParser.ArrayValue deckRedArr = rootOb.entries["deckRed"] as JsonParser.ArrayValue;
            for (int idx = 0; idx < deckRedArr.entries.Count; idx++)
            {
                configData.deckRed[idx] = ParseConfigCardData(deckRedArr.entries[idx] as JsonParser.ObjectValue);
            }

            JsonParser.ArrayValue deckBlueArr = rootOb.entries["deckBlue"] as JsonParser.ArrayValue;
            for (int idx = 0; idx < deckBlueArr.entries.Count; idx++)
            {
                configData.deckBlue[idx] = ParseConfigCardData(deckBlueArr.entries[idx] as JsonParser.ObjectValue);
            }

            JsonParser.ArrayValue boardArr = rootOb.entries["board"] as JsonParser.ArrayValue;
            for (int idx = 0; idx < boardArr.entries.Count; idx++)
            {
                configData.board[idx] = ParseConfigCardData(boardArr.entries[idx] as JsonParser.ObjectValue);
            }

            return configData;
        }

        private static void RunVerify(string configPath, string imagePath, bool bDebugMode)
        {
            string testName = Path.GetFileNameWithoutExtension(configPath);
            Logger.WriteLine("==> Verify: " + testName);

            VerifyConfig configData = LoadConfigData(configPath);

            ScreenshotAnalyzer.EMode scanMode = ScreenshotAnalyzer.EMode.All | ScreenshotAnalyzer.EMode.DebugForceCached;
            if (bDebugMode)
            {
                scanMode |= ScreenshotAnalyzer.EMode.Debug;
            }

            ScreenshotAnalyzer screenReader = new ScreenshotAnalyzer();
            screenReader.testScreenshotPath = imagePath;
            screenReader.DoWork(scanMode);

            // fixup missing rules
            int numAddedRules = 0;
            for (int ruleIdx = 0; ruleIdx < configData.rules.Length; ruleIdx++)
            {
                int readRuleIdx = ruleIdx - numAddedRules;
                bool hasMatchingRule = false;

                if (readRuleIdx < screenReader.currentTriadGame.mods.Count)
                {
                    string readRuleName = screenReader.currentTriadGame.mods[readRuleIdx].GetName();
                    hasMatchingRule = readRuleName == configData.rules[ruleIdx];
                }

                if (!hasMatchingRule)
                {
                    if (screenReader.unknownHashes.Count > 0)
                    {
                        ImageHashData hashData = new ImageHashData(mapRules[configData.rules[ruleIdx]], screenReader.unknownHashes[0].hashData.Hash, screenReader.unknownHashes[0].hashData.Type);
                        PlayerSettingsDB.Get().AddKnownHash(hashData);
                        PlayerSettingsDB.Get().Save();

                        screenReader.PopUnknownHash();
                        numAddedRules++;
                    }
                    else
                    {
                        string exceptionMsg = string.Format("Test {0} failed! Can't match rules!", testName);
                        throw new Exception(exceptionMsg);
                    }
                }
            }

            // verify decks
            TriadCard lockedBlueCard = screenReader.currentTriadGame.forcedBlueCard;
            for (int idx = 0; idx < 5; idx++)
            {
                TriadCard blueCard = screenReader.currentTriadGame.blueDeck[idx];
                ECardState blueState =
                    (blueCard == null) ? ECardState.None :
                    blueCard.Name == "failedMatch" ? ECardState.Visible :
                    !blueCard.IsValid() ? ECardState.Hidden :
                    (lockedBlueCard == null || lockedBlueCard == blueCard) ? ECardState.Visible :
                    ECardState.Locked;

                if (blueState != configData.deckBlue[idx].state)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Deck Blue[{1}] got:{2}, expected:{3}",
                        testName, idx,
                        blueState,
                        configData.deckBlue[idx].state);
                    throw new Exception(exceptionMsg);
                }

                if (blueCard != null &&
                    (blueCard.Sides[0] != configData.deckBlue[idx].sides[0] ||
                    blueCard.Sides[1] != configData.deckBlue[idx].sides[1] ||
                    blueCard.Sides[2] != configData.deckBlue[idx].sides[2] ||
                    blueCard.Sides[3] != configData.deckBlue[idx].sides[3]))
                {
                    string exceptionMsg = string.Format("Test {0} failed! Deck Blue[{1}] got:[{2},{3},{4},{5}], expected:[{6},{7},{8},{9}]",
                        testName, idx,
                        blueCard.Sides[0], blueCard.Sides[1], blueCard.Sides[2], blueCard.Sides[3],
                        configData.deckBlue[idx].sides[0], configData.deckBlue[idx].sides[1], configData.deckBlue[idx].sides[2], configData.deckBlue[idx].sides[3]);
                    throw new Exception(exceptionMsg);
                }

                TriadCard redCard = screenReader.currentTriadGame.redDeck[idx];
                ECardState redState =
                    (redCard == null) ? ECardState.None :
                    redCard.Name == "failedMatch" ? ECardState.Visible :
                    !redCard.IsValid() ? ECardState.Hidden :
                    ECardState.Visible;

                if (configData.deckRed[idx].state == ECardState.Locked) { configData.deckRed[idx].state = ECardState.Visible; }
                if (redState != configData.deckRed[idx].state)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Deck Red[{1}] got:{2}, expected:{3}",
                        testName, idx,
                        redState,
                        configData.deckRed[idx].state);
                    throw new Exception(exceptionMsg);
                }

                if (redCard != null &&
                    (redCard.Sides[0] != configData.deckRed[idx].sides[0] ||
                    redCard.Sides[1] != configData.deckRed[idx].sides[1] ||
                    redCard.Sides[2] != configData.deckRed[idx].sides[2] ||
                    redCard.Sides[3] != configData.deckRed[idx].sides[3]))
                {
                    string exceptionMsg = string.Format("Test {0} failed! Deck Red[{1}] got:[{2},{3},{4},{5}], expected:[{6},{7},{8},{9}]",
                        testName, idx,
                        redCard.Sides[0], redCard.Sides[1], redCard.Sides[2], redCard.Sides[3],
                        configData.deckRed[idx].sides[0], configData.deckRed[idx].sides[1], configData.deckRed[idx].sides[2], configData.deckRed[idx].sides[3]);
                    throw new Exception(exceptionMsg);
                }
            }

            // verify board
            for (int idx = 0; idx < 9; idx++)
            {
                TriadCard testCard = screenReader.currentTriadGame.board[idx];
                ECardState cardState =
                    (testCard == null) ? ECardState.None :
                    screenReader.currentTriadGame.boardOwner[idx] == ETriadCardOwner.Red ? ECardState.PlacedRed :
                    screenReader.currentTriadGame.boardOwner[idx] == ETriadCardOwner.Blue ? ECardState.PlacedBlue :
                    ECardState.None;

                if (cardState != configData.board[idx].state)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Board[{1}] got:{2}, expected:{3}",
                        testName, idx,
                        cardState,
                        configData.board[idx].state);
                    throw new Exception(exceptionMsg);
                }

                if (testCard != null &&
                    (testCard.Sides[0] != configData.board[idx].sides[0] ||
                    testCard.Sides[1] != configData.board[idx].sides[1] ||
                    testCard.Sides[2] != configData.board[idx].sides[2] ||
                    testCard.Sides[3] != configData.board[idx].sides[3]))
                {
                    string exceptionMsg = string.Format("Test {0} failed! Board[{1}] got:[{2},{3},{4},{5}], expected:[{6},{7},{8},{9}]",
                        testName, idx,
                        testCard.Sides[0], testCard.Sides[1], testCard.Sides[2], testCard.Sides[3],
                        configData.board[idx].sides[0], configData.board[idx].sides[1], configData.board[idx].sides[2], configData.board[idx].sides[3]);
                    throw new Exception(exceptionMsg);
                }
            }
        }
#endif // DEBUG
    }
}
