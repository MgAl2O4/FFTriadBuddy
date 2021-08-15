using MgAl2O4.Utils;
using System;
using System.Collections.Generic;

namespace FFTriadBuddy
{
    public class TriadGameScreenTests
    {
        private static Dictionary<string, TriadGameModifier> mapValidationRules;

        public static void RunTest(string configPath, bool debugMode)
        {
            string testName = System.IO.Path.GetFileNameWithoutExtension(configPath);

            string configText = System.IO.File.ReadAllText(configPath);
            JsonParser.ObjectValue rootOb = JsonParser.ParseJson(configText);
            if (rootOb["type"] != "Screen")
            {
                return;
            }

            ScannerTriad.VerifyConfig configData = new ScannerTriad.VerifyConfig();
            configData.Load(rootOb);

            // setup npc & modifiers
            TriadNpc testNpc = TriadNpcDB.Get().Find(configData.npc);
            if (testNpc == null)
            {
                string exceptionMsg = string.Format("Test {0} failed! Can't find npc: {1}", testName, configData.npc);
                throw new Exception(exceptionMsg);
            }

            ScannerTriad.GameState screenGame = new ScannerTriad.GameState();
            if (mapValidationRules == null)
            {
                mapValidationRules = new Dictionary<string, TriadGameModifier>();
                foreach (TriadGameModifier mod in ImageHashDB.Get().modObjects)
                {
                    mapValidationRules.Add(mod.GetCodeName(), mod);
                }
            }

            foreach (string modName in configData.rules)
            {
                screenGame.mods.Add(mapValidationRules[modName]);
            }

            Func<ScannerTriad.VerifyCard, TriadCard> ConvertToTriadCard = configCard =>
            {
                if (configCard.state == ScannerTriad.ECardState.None) { return null; }
                if (configCard.state == ScannerTriad.ECardState.Hidden) { return TriadCardDB.Get().hiddenCard; }

                TriadCard matchingCard = !string.IsNullOrEmpty(configCard.name) ?
                    TriadCardDB.Get().Find(configCard.name) :
                    TriadCardDB.Get().Find(configCard.sides[0], configCard.sides[1], configCard.sides[2], configCard.sides[3]);

                if (matchingCard == null)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Can't match validation card: '{1}' [{2},{3},{4},{5}]", testName,
                        configCard.name, configCard.sides[0], configCard.sides[1], configCard.sides[2], configCard.sides[3]);
                    throw new Exception(exceptionMsg);
                }

                return matchingCard;
            };

            bool needsLockedBlue = false;
            for (int idx = 0; idx < 5; idx++)
            {
                screenGame.blueDeck[idx] = ConvertToTriadCard(configData.deckBlue[idx]);
                screenGame.redDeck[idx] = ConvertToTriadCard(configData.deckRed[idx]);

                if (configData.deckBlue[idx].state == ScannerTriad.ECardState.Locked)
                {
                    needsLockedBlue = true;
                }
            }

            if (needsLockedBlue)
            {
                for (int idx = 0; idx < 5; idx++)
                {
                    if (configData.deckBlue[idx].state == ScannerTriad.ECardState.Visible)
                    {
                        screenGame.forcedBlueCard = screenGame.blueDeck[idx];
                        break;
                    }
                }
            }

            for (int idx = 0; idx < 9; idx++)
            {
                screenGame.board[idx] = ConvertToTriadCard(configData.board[idx]);
                screenGame.boardOwner[idx] =
                    configData.board[idx].state == ScannerTriad.ECardState.PlacedBlue ? ETriadCardOwner.Blue :
                    configData.board[idx].state == ScannerTriad.ECardState.PlacedRed ? ETriadCardOwner.Red :
                    ETriadCardOwner.Unknown;
            }

            TriadGameScreenMemory screenMemory = new TriadGameScreenMemory { logScan = false };
            screenMemory.OnNewScan(screenGame, testNpc);
            screenMemory.gameSession.SolverFindBestMove(screenMemory.gameState, out int solverBoardPos, out TriadCard solverTriadCard, out TriadGameResultChance bestChance);

            if (bestChance.expectedResult == ETriadGameState.BlueLost && bestChance.winChance <= 0.0f && bestChance.drawChance <= 0.0f)
            {
                string exceptionMsg = string.Format("Test {0} failed! Can't find move!", testName);
                throw new Exception(exceptionMsg);
            }
        }
    }
}
