using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FFTriadBuddy
{
    public class TriadGameTests
    {
        private static Dictionary<string, TriadGameModifier> mapValidationRules;

        private class VerifyMove
        {
            private ETriadCardOwner[] expectedState;
            public TriadCard card;
            public ETriadCardOwner owner;
            public int boardPos;

            public void Load(JsonParser.ObjectValue configOb)
            {
                string ownerStr = configOb["player"] as JsonParser.StringValue;
                owner = (ownerStr == "blue") ? ETriadCardOwner.Blue : (ownerStr == "red") ? ETriadCardOwner.Red : ETriadCardOwner.Unknown;

                boardPos = configOb["pos"] as JsonParser.IntValue;

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

            public bool VerifyState(TriadGameSimulationState gameState, bool debugMode)
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

        public static void RunTest(string configPath, bool debugMode)
        {
            string testName = System.IO.Path.GetFileNameWithoutExtension(configPath);

            string configText = System.IO.File.ReadAllText(configPath);
            JsonParser.ObjectValue configOb = JsonParser.ParseJson(configText);
            if (configOb["type"] != "Solver")
            {
                return;
            }

            // intial state
            ScannerTriad.VerifyConfig configData = new ScannerTriad.VerifyConfig();
            configData.Load(configOb);

            if (mapValidationRules == null)
            {
                mapValidationRules = new Dictionary<string, TriadGameModifier>();
                foreach (TriadGameModifier mod in ImageHashDB.Get().modObjects)
                {
                    mapValidationRules.Add(mod.GetCodeName(), mod);
                }
            }

            List<TriadGameModifier> configMods = new List<TriadGameModifier>();
            foreach (string modName in configData.rules)
            {
                configMods.Add(mapValidationRules[modName]);
            }

            TriadGameSimulation testSession = new TriadGameSimulation();
            testSession.Initialize(configMods);

            TriadGameSimulationState testGameData = new TriadGameSimulationState() { bDebugRules = debugMode };

            if (configData.board.Length > 0)
            {
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

                for (int idx = 0; idx < configData.board.Length; idx++)
                {
                    var configState = configData.board[idx].state;
                    if (configState != ScannerTriad.ECardState.None)
                    {
                        testGameData.board[idx] = new TriadCardInstance(ConvertToTriadCard(configData.board[idx]),
                            (configState == ScannerTriad.ECardState.PlacedBlue) ? ETriadCardOwner.Blue :
                            (configState == ScannerTriad.ECardState.PlacedRed) ? ETriadCardOwner.Red :
                            ETriadCardOwner.Unknown);
                    }
                }
            }

            var deckRed = new TriadDeck();
            var deckBlue = new TriadDeck();

            testGameData.deckBlue = new TriadDeckInstanceManual(deckBlue);
            testGameData.deckRed = new TriadDeckInstanceManual(deckRed);

            JsonParser.ArrayValue moveArr = configOb.entries["moves"] as JsonParser.ArrayValue;
            for (int idx = 0; idx < moveArr.entries.Count; idx++)
            {
                var move = new VerifyMove();
                move.Load(moveArr.entries[idx] as JsonParser.ObjectValue);

                var useDeck = (move.owner == ETriadCardOwner.Blue) ? deckBlue : deckRed;
                useDeck.knownCards.Add(move.card);
                if (idx == 0)
                {
                    testGameData.state = (move.owner == ETriadCardOwner.Blue) ? ETriadGameState.InProgressBlue : ETriadGameState.InProgressRed;
                }

                if (debugMode) { Logger.WriteLine("move[{0}]: [{1}] {2}: {3}", idx, move.boardPos, move.owner, move.card); }

                bool result = testSession.PlaceCard(testGameData, move.card, move.owner, move.boardPos);
                if (!result)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Can't place card!", testName);
                    throw new Exception(exceptionMsg);
                }

                result = move.VerifyState(testGameData, debugMode);
                if (!result)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Finished with bad state!", testName);
                    throw new Exception(exceptionMsg);
                }
            }
        }

        public static void RunSolverStressTest()
        {
            int numIterations = 1000 * 1000;
            Logger.WriteLine("Solver speed testing start, numIterations:" + numIterations);

            Stopwatch timer = new Stopwatch();
            timer.Start();

            TriadDeck testDeck = new TriadDeck(new int[] { 10, 20, 30, 40, 50 });
            TriadNpc testNpc = TriadNpcDB.Get().Find("Garima");

            var solver = new TriadGameSolver();
            solver.InitializeSimulation(testNpc.Rules);

            var agent = new TriadGameAgentRandom(solver, 0);
            for (int Idx = 0; Idx < numIterations; Idx++)
            {
                var gameState = solver.StartSimulation(testDeck, testNpc.Deck, ETriadGameState.InProgressBlue);
                solver.RunSimulation(gameState, agent, agent);
            }

            timer.Stop();
            Logger.WriteLine("Solver speed testing finished, time taken:" + timer.ElapsedMilliseconds + "ms");

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

        private static int[][] BuildDeckPermutations()
        {
            var permutationList = new int[120][];
            int ListIdx = 0;
            for (int IdxP0 = 0; IdxP0 < 5; IdxP0++)
            {
                for (int IdxP1 = 0; IdxP1 < 5; IdxP1++)
                {
                    if (IdxP1 == IdxP0) { continue; }
                    for (int IdxP2 = 0; IdxP2 < 5; IdxP2++)
                    {
                        if (IdxP2 == IdxP0 || IdxP2 == IdxP1) { continue; }
                        for (int IdxP3 = 0; IdxP3 < 5; IdxP3++)
                        {
                            if (IdxP3 == IdxP0 || IdxP3 == IdxP1 || IdxP3 == IdxP2) { continue; }
                            for (int IdxP4 = 0; IdxP4 < 5; IdxP4++)
                            {
                                if (IdxP4 == IdxP0 || IdxP4 == IdxP1 || IdxP4 == IdxP2 || IdxP4 == IdxP3) { continue; }

                                permutationList[ListIdx] = new int[5] { IdxP0, IdxP1, IdxP2, IdxP3, IdxP4 };
                                ListIdx++;
                            }
                        }
                    }
                }
            }

            return permutationList;
        }

        public static void RunSolverAccuracyTests()
        {
            int numIterations = 200;
            var deckPermutations = BuildDeckPermutations();

            Logger.WriteLine("Solver accuracy testing start, numIterations:" + numIterations);

            Stopwatch timer = new Stopwatch();
            timer.Start();

            TriadDeck testDeck = new TriadDeck(new int[] { 61, 248, 113, 191, 87 });
            TriadNpc testNpc = TriadNpcDB.Get().Find("Garima");
            //TriadNpc testNpc = TriadNpcDB.Get().Find("Swift");

            var solver = new TriadGameSolver();
            solver.InitializeSimulation(testNpc.Rules);

            // agent to test for accuracy
            //var agentPlayer = new TriadGameAgentDerpyCarlo();
            var agentPlayer = new TriadGameAgentCarloTheExplorer();
            var agentVs = new TriadGameAgentRandom();

            var sessionRand = new Random(0);
            agentPlayer.Initialize(solver, sessionRand.Next());
            agentVs.Initialize(solver, sessionRand.Next());

            bool hasChaosRule = solver.HasSimulationRule(ETriadGameSpecialMod.BlueCardSelection);

            int numControlledCards = 0;
            int numWins = 0;
            for (int Idx = 0; Idx < numIterations; Idx++)
            {
                var gameState = solver.StartSimulation(testDeck, testNpc.Deck, ETriadGameState.InProgressRed);

                int[] blueDeckOrder = null;
                int blueDeckIdx = 0;
                if (hasChaosRule)
                {
                    int permIdx = sessionRand.Next() % deckPermutations.Length;
                    blueDeckOrder = deckPermutations[permIdx];
                }

                if (Idx > 0 && Idx % 20 == 0)
                {
                    Logger.WriteLine(">> {0}/{1}", Idx, numIterations);
                }

                bool keepPlaying = true;
                while (keepPlaying)
                {
                    gameState.forcedCardIdx = -1;

                    if (gameState.state == ETriadGameState.InProgressBlue)
                    {
                        if (blueDeckOrder != null)
                        {
                            gameState.forcedCardIdx = blueDeckOrder[blueDeckIdx];
                            blueDeckIdx++;
                        }

                        keepPlaying = agentPlayer.FindNextMove(solver, gameState, out int cardIdx, out int boardPos, out var dummyResult);
                        if (keepPlaying)
                        {
                            keepPlaying = solver.simulation.PlaceCard(gameState, cardIdx, gameState.deckBlue, ETriadCardOwner.Blue, boardPos);
                        }
                    }
                    else if (gameState.state == ETriadGameState.InProgressRed)
                    {
                        keepPlaying = agentVs.FindNextMove(solver, gameState, out int cardIdx, out int boardPos, out var dummyResult);
                        if (keepPlaying)
                        {
                            keepPlaying = solver.simulation.PlaceCard(gameState, cardIdx, gameState.deckRed, ETriadCardOwner.Red, boardPos);
                        }
                    }
                    else
                    {
                        keepPlaying = false;
                    }
                }

                int numBlue = (gameState.deckBlue.availableCardMask != 0) ? 1 : 0;
                foreach (TriadCardInstance card in gameState.board)
                {
                    numBlue += (card != null && card.owner == ETriadCardOwner.Blue) ? 1 : 0;
                }

                numControlledCards += numBlue;
                numWins += (gameState.state == ETriadGameState.BlueWins) ? 1 : 0;
            }

            timer.Stop();
            Logger.WriteLine("Solver accuracy testing finished, score:{0:P2}, control:{1:0.##}, time taken:{2}s",
                (float)numWins / numIterations,
                (float)numControlledCards / numIterations,
                timer.ElapsedMilliseconds / 1000.0f);

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
    }
}
