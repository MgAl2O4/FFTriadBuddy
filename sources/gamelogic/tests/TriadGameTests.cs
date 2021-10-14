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

        private static int[][] deckPermutations;
        private static int[] PickRandomPermutation(Random rand)
        {
            if (deckPermutations == null)
            {
                deckPermutations = BuildDeckPermutations();
            }

            return deckPermutations[rand.Next(deckPermutations.Length)];
        }

        class SolverAccTestInfo
        {
            public TriadGameAgent agentBlue;
            public TriadGameAgent agentRed;
            public int numWins = 0;
            public int numControlled = 0;
            public float elapsedSeconds = 0.0f;
            public List<float> predictionSteps;
        }

        private static void PlayTestGame(SolverAccTestInfo testInfo, TriadGameSolver solver, TriadGameSimulationState gameState, Random sessionRand)
        {
            int[] blueDeckOrder = solver.HasSimulationRule(ETriadGameSpecialMod.BlueCardSelection) ? PickRandomPermutation(sessionRand) : null;

            bool keepPlaying = true;
            while (keepPlaying)
            {
                if (gameState.state == ETriadGameState.InProgressBlue)
                {
                    gameState.forcedCardIdx = (blueDeckOrder != null) ? blueDeckOrder[gameState.deckBlue.numPlaced] : -1;

                    keepPlaying = testInfo.agentBlue.FindNextMove(solver, gameState, out int cardIdx, out int boardPos, out var blueResult);
                    if (keepPlaying)
                    {
                        testInfo.predictionSteps[gameState.deckBlue.numPlaced] += blueResult.winChance;

                        keepPlaying = solver.simulation.PlaceCard(gameState, cardIdx, gameState.deckBlue, ETriadCardOwner.Blue, boardPos);
                    }
                }
                else if (gameState.state == ETriadGameState.InProgressRed)
                {
                    gameState.forcedCardIdx = -1;

                    keepPlaying = testInfo.agentRed.FindNextMove(solver, gameState, out int cardIdx, out int boardPos, out var dummyResult);
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

            testInfo.numControlled += numBlue;
            testInfo.numWins += (gameState.state == ETriadGameState.BlueWins) ? 1 : 0;
        }

        public static void RunSolverAccuracyTests()
        {
            int numIterations = 200;
            var deckPermutations = BuildDeckPermutations();

            Logger.WriteLine("Solver accuracy testing start, numIterations:" + numIterations);

            var deckRand = new Random(20);
            var deckCards = new int[5] { 61, 248, 113, 191, 87 };
            var cardDB = TriadCardDB.Get();
            int idx = 0;
            while (idx < deckCards.Length)
            {
                int cardIdx = deckRand.Next(cardDB.cards.Count);
                if (cardDB.cards[cardIdx].IsValid())
                {
                    deckCards[idx] = cardIdx;
                    idx++;
                }
            }

            TriadDeck testDeck = new TriadDeck(deckCards);
            //TriadNpc testNpc = TriadNpcDB.Get().Find("Garima");
            //TriadNpc testNpc = TriadNpcDB.Get().Find("Swift");
            TriadNpc testNpc = TriadNpcDB.Get().Find("Aurifort of the Three Clubs");

            var solver = new TriadGameSolver();
            solver.InitializeSimulation(testNpc.Rules);

            var playerAgents = new List<TriadGameAgent>();
            playerAgents.Add(new TriadGameAgentDerpyCarlo());
            playerAgents.Add(new TriadGameAgentCarloTheExplorer());
            playerAgents.Add(new TriadGameAgentCarloScored());

            // single iteration: in depth testing for single agent
            if (numIterations == 1)
            {
                playerAgents.Clear();
                playerAgents.Add(new TriadGameAgentCarloScored());

                playerAgents[0].debugFlags = TriadGameAgent.DebugFlags.AgentInitialize | TriadGameAgent.DebugFlags.ShowMoveStart | TriadGameAgent.DebugFlags.ShowMoveDetails;
            }

            var testResults = new List<SolverAccTestInfo>();
            foreach (var agent in playerAgents)
            {
                var testInfo = new SolverAccTestInfo() { agentBlue = agent, agentRed = new TriadGameAgentRandom() };
                testInfo.agentBlue.Initialize(solver, 0);
                testInfo.agentRed.Initialize(solver, 0);

                testInfo.predictionSteps = new List<float>();
                for (idx = 0; idx < 5; idx++)
                {
                    testInfo.predictionSteps.Add(0.0f);
                }

                testResults.Add(testInfo);
            }

            var iterCounter = 0;
            foreach (var testInfo in testResults)
            {
                var sessionRand = new Random(0);
                Stopwatch timer = new Stopwatch();
                timer.Start();

                for (int Idx = 0; Idx < numIterations; Idx++)
                {
                    iterCounter++;
                    if (iterCounter % 20 == 0)
                    {
                        Logger.WriteLine(">> {0}/{1}", iterCounter, numIterations * testResults.Count);
                    }

                    var initialState = solver.StartSimulation(testDeck, testNpc.Deck, ETriadGameState.InProgressRed);
                    PlayTestGame(testInfo, solver, initialState, sessionRand);
                }

                timer.Stop();
                testInfo.elapsedSeconds = timer.ElapsedMilliseconds / 1000.0f;
            }

            Logger.WriteLine("Solver accuracy testing finished");
            foreach (var testInfo in testResults)
            {
                string predictionDesc = "";
                for (idx = 0; idx < testInfo.predictionSteps.Count - 1; idx++)
                {
                    if (predictionDesc.Length > 0) { predictionDesc += ", "; }
                    predictionDesc += $"{(testInfo.predictionSteps[idx] / numIterations):P0}";
                }

                Logger.WriteLine("[{0}] score:{1:P2}, control:{2:0.##}, time taken:{3}s, predictions:{4}",
                    testInfo.agentBlue.agentName,
                    (float)testInfo.numWins / numIterations,
                    (float)testInfo.numControlled / numIterations,
                    testInfo.elapsedSeconds,
                    predictionDesc);
            }

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

        private static TriadNpc PickRandomNpc(Random rand)
        {
            var npcList = TriadNpcDB.Get().npcs;
            TriadNpc npcOb = null;
            while (npcOb == null)
            {
                npcOb = npcList[rand.Next(npcList.Count)];
            }

            return npcOb;
        }

        private static Dictionary<int, List<TriadCard>> mapCardRarities;
        private static TriadCard PickRandomCard(Random rand, int rarity)
        {
            if (mapCardRarities == null)
            {
                mapCardRarities = new Dictionary<int, List<TriadCard>>();
                for (int idx = 0; idx < 5; idx++)
                {
                    mapCardRarities.Add(idx, new List<TriadCard>());
                }

                var cardList = TriadCardDB.Get().cards;
                foreach (var card in cardList)
                {
                    if (card != null && card.IsValid())
                    {
                        mapCardRarities[(int)card.Rarity].Add(card);
                    }
                }
            }

            var rarityList = mapCardRarities[rarity];
            return rarityList[rand.Next(rarityList.Count)];
        }

        private static TriadDeck PickRandomDeck(Random rand)
        {
            int[] rarityCount = new int[5];
            int rarityType = rand.Next(10);
            if (rarityType < 2)
            {
                rarityCount[0] = 4;
                rarityCount[1] = 1;
            }
            else if (rarityType < 5)
            {
                rarityCount[0] = 1;
                rarityCount[1] = 2;
                rarityCount[2] = 1;
                rarityCount[3] = 1;
                rarityCount[4] = 0;
            }
            else
            {
                rarityCount[2] = 3;
                rarityCount[3] = 1;
                rarityCount[4] = 1;
            }

            var listCards = new List<TriadCard>();
            for (int idxRarity = 0; idxRarity < rarityCount.Length; idxRarity++)
            {
                int numAdded = 0;
                while (numAdded < rarityCount[idxRarity])
                {
                    var cardOb = PickRandomCard(rand, idxRarity);
                    if (!listCards.Contains(cardOb))
                    {
                        listCards.Add(cardOb);
                        numAdded++;
                    }
                }
            }

            return new TriadDeck(listCards);
        }

        public static void GenerateAccuracyTrainingData()
        {
            int numGames = 100;
            int numSamples = 200;
            int seed = 0;

            TriadGameAgentRandom.UseEqualDistribution = true;
            var rand = new Random(seed);
            
            var testLines = new List<string>();
            testLines.Add("seed,deck0,deck1,deck2,deck3,deck4,npc,winChance");

            for (int idxSample = 0; idxSample < numSamples; idxSample++)
            {
                Logger.WriteLine($">> {idxSample + 1}/{numSamples}");

                var sessionSeed = rand.Next();
                var sessionRand = new Random(sessionSeed);

                var testNpc = PickRandomNpc(sessionRand);
                var testDeck = PickRandomDeck(sessionRand);

                var solver = new TriadGameSolver();
                solver.InitializeSimulation(testNpc.Rules);

                var testInfo = new SolverAccTestInfo() { agentBlue = new TriadGameAgentCarloTheExplorer(), agentRed = new TriadGameAgentRandom() };
                testInfo.agentBlue.Initialize(solver, 0);
                testInfo.agentRed.Initialize(solver, 0);

                testInfo.predictionSteps = new List<float>();
                for (int idx = 0; idx < 5; idx++)
                {
                    testInfo.predictionSteps.Add(0.0f);
                }

                for (int idxGame = 0; idxGame < numGames; idxGame++)
                {
                    var initialState = solver.StartSimulation(testDeck, testNpc.Deck, ETriadGameState.InProgressRed);
                    PlayTestGame(testInfo, solver, initialState, sessionRand);
                }

                var winChance = 1.0f * testInfo.numWins / numGames;
                testLines.Add($"{sessionSeed},{testDeck.knownCards[0].Id},{testDeck.knownCards[1].Id},{testDeck.knownCards[2].Id},{testDeck.knownCards[3].Id},{testDeck.knownCards[4].Id},{testNpc.Id},{winChance}");
            }

            System.IO.File.WriteAllLines("predictionDump.csv", testLines);
        }
    }
}
