using MgAl2O4.Utils;
using System;
using System.Collections.Generic;

namespace FFTriadBuddy
{
    public class TriadGameScreenTests
    {
        private static Dictionary<string, TriadGameModifier> mapValidationRules;

        private class VerifyMove
        {
            private ETriadCardOwner[] expectedState;
            public TriadCard card;
            public ETriadCardOwner owner;
            public int boardPos;
            public int cardIdx;

            public void Load(JsonParser.ObjectValue configOb)
            {
                string ownerStr = configOb["player"] as JsonParser.StringValue;
                owner = (ownerStr == "blue") ? ETriadCardOwner.Blue : (ownerStr == "red") ? ETriadCardOwner.Red : ETriadCardOwner.Unknown;

                boardPos = configOb["pos"] as JsonParser.IntValue;
                cardIdx = configOb["cardIdx"] as JsonParser.IntValue;

                if (configOb.entries.ContainsKey("board"))
                {
                    string boardCode = configOb["board"] as JsonParser.StringValue;
                    boardCode = boardCode.Replace(" ", "");

                    expectedState = new ETriadCardOwner[9];
                    for (int idx = 0; idx < expectedState.Length; idx++)
                    {
                        expectedState[idx] = (boardCode[idx] == 'R') ? ETriadCardOwner.Red : (boardCode[idx] == 'B') ? ETriadCardOwner.Blue : ETriadCardOwner.Unknown;
                    }
                }

                var cardName = configOb["card"] as JsonParser.StringValue;
                if (cardName != null)
                {
                    card = TriadCardDB.Get().Find(cardName);
                }
                else
                {
                    var cardSides = configOb["card"] as JsonParser.ArrayValue;

                    int numU = cardSides[0] as JsonParser.IntValue;
                    int numL = cardSides[1] as JsonParser.IntValue;
                    int numD = cardSides[2] as JsonParser.IntValue;
                    int numR = cardSides[3] as JsonParser.IntValue;

                    card = TriadCardDB.Get().Find(numU, numL, numD, numR);
                }
            }

            public bool VerifyState(TriadGameData gameState, bool debugMode)
            {
                if (expectedState != null)
                {
                    for (int idx = 0; idx < expectedState.Length; idx++)
                    {
                        if (gameState.board[idx].owner != expectedState[idx])
                        {
                            if (debugMode)
                            {
                                string expectedCode = "";
                                string currentCode = "";
                                Func<ETriadCardOwner, char> GetOwnerCode = (owner) => (owner == ETriadCardOwner.Blue) ? 'B' : (owner == ETriadCardOwner.Red) ? 'R' : '.';

                                for (int codeIdx = 0; codeIdx < 9; codeIdx++)
                                {
                                    if (codeIdx == 3 || codeIdx == 6) { expectedCode += ' '; currentCode += ' '; }

                                    expectedCode += GetOwnerCode(gameState.board[codeIdx].owner);
                                    currentCode += GetOwnerCode(expectedState[codeIdx]);
                                }

                                Logger.WriteLine("Failed, mismatch at [{0}]! Expected:{1}, got{2}", idx, expectedCode, currentCode);
                            }
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        private static void CopyGameStateToScreen(TriadGameData testGameData, ScannerTriad.GameState screenGame)
        {
            for (int idx = 0; idx < 9; idx++)
            {
                screenGame.board[idx] = testGameData.board[idx] != null ? testGameData.board[idx].card : null;
                screenGame.boardOwner[idx] = testGameData.board[idx] != null ? testGameData.board[idx].owner : ETriadCardOwner.Unknown;
            }

            for (int idx = 0; idx < 5; idx++)
            {
                screenGame.blueDeck[idx] = testGameData.deckBlue.GetCard(idx);

                if (testGameData.deckRed.IsPlaced(idx))
                {
                    screenGame.redDeck[idx] = null;
                }
            }
        }

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
            if (!rootOb.entries.ContainsKey("moves"))
            {
                screenMemory.OnNewScan(screenGame, testNpc);
                screenMemory.gameSession.SolverFindBestMove(screenMemory.gameState, out int solverBoardPos, out TriadCard solverTriadCard, out TriadGameResultChance bestChance);

                if (bestChance.expectedResult == ETriadGameState.BlueLost && bestChance.winChance <= 0.0f && bestChance.drawChance <= 0.0f)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Can't find move!", testName);
                    throw new Exception(exceptionMsg);
                }
            }
            else
            {
                debugMode = true;

                TriadGameSession testSession = new TriadGameSession();
                testSession.modifiers = screenGame.mods;
                testSession.UpdateSpecialRules();

                TriadGameData testGameData = new TriadGameData() { bDebugRules = debugMode };
                testGameData.deckBlue = new TriadDeckInstanceManual(new TriadDeck(screenGame.blueDeck));
                testGameData.deckRed = new TriadDeckInstanceManual(testNpc.Deck);

                bool shouldForceBlueSelection = true;

                JsonParser.ArrayValue moveArr = rootOb.entries["moves"] as JsonParser.ArrayValue;
                for (int idx = 0; idx < moveArr.entries.Count; idx++)
                {
                    var move = new VerifyMove();
                    move.Load(moveArr.entries[idx] as JsonParser.ObjectValue);

                    if (debugMode) { Logger.WriteLine("move[{0}]: [{1}] {2}: {3}", idx, move.boardPos, move.owner, move.card); }

                    if (move.owner == ETriadCardOwner.Blue)
                    {
                        CopyGameStateToScreen(testGameData, screenGame);
                        if (shouldForceBlueSelection) 
                        {
                            screenGame.forcedBlueCard = screenGame.blueDeck[move.cardIdx];
                        }

                        screenMemory.OnNewScan(screenGame, testNpc);
                        screenMemory.gameSession.SolverFindBestMove(screenMemory.gameState, out int solverBoardPos, out TriadCard solverTriadCard, out TriadGameResultChance bestChance);

                        if (debugMode)
                        {
                            Logger.WriteLine("solver: {0} -> board[{1}], chance: {2}", solverTriadCard.Name.GetCodeName(), solverBoardPos, bestChance.expectedResult);

                            if (solverBoardPos != move.boardPos)
                            {
                                Logger.WriteLine("  >> MISMATCH!");
                            }
                        }
                    }

                    testGameData.state = move.owner == ETriadCardOwner.Blue ? ETriadGameState.InProgressBlue : ETriadGameState.InProgressRed;
                    testSession.PlaceCard(testGameData, move.card, move.owner, move.boardPos);
                }
            }
        }
    }
}
