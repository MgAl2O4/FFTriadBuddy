using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public enum ETriadGameState
    {
        InProgressBlue,
        InProgressRed,
        BlueWins,
        BlueDraw,
        BlueLost,
    }

    public class TriadGameData
    {
        public TriadCardInstance[] board;
        public TriadDeckInstance deckBlue;
        public TriadDeckInstance deckRed;
        public ETriadGameState state;
        public ETriadGameSpecialMod resolvedSpecial;
        public int[] typeMods;
        public int numCardsPlaced;
        public int numRestarts;
        public int forcedCardIdx;
        public bool bDebugRules;

        public const int boardSize = 3;

        public TriadGameData()
        {
            board = new TriadCardInstance[boardSize * boardSize];
            typeMods = new int[Enum.GetNames(typeof(ETriadCardType)).Length];
            state = ETriadGameState.InProgressBlue;
            resolvedSpecial = ETriadGameSpecialMod.None;
            numCardsPlaced = 0;
            numRestarts = 0;
            forcedCardIdx = -1;
            bDebugRules = false;

            for (int Idx = 0; Idx < typeMods.Length; Idx++)
            {
                typeMods[Idx] = 0;
            }
        }

        public TriadGameData(TriadGameData copyFrom)
        {
            board = new TriadCardInstance[copyFrom.board.Length];
            for (int Idx = 0; Idx < board.Length; Idx++)
            {
                board[Idx] = (copyFrom.board[Idx] == null) ? null : new TriadCardInstance(copyFrom.board[Idx]);
            }

            typeMods = new int[copyFrom.typeMods.Length];
            for (int Idx = 0; Idx < typeMods.Length; Idx++)
            {
                typeMods[Idx] = copyFrom.typeMods[Idx];
            }

            deckBlue = copyFrom.deckBlue.CreateCopy();
            deckRed = copyFrom.deckRed.CreateCopy();
            state = copyFrom.state;
            numCardsPlaced = copyFrom.numCardsPlaced;
            numRestarts = copyFrom.numRestarts;
            resolvedSpecial = copyFrom.resolvedSpecial;
            // bDebugRules not copied, only first step needs it
        }
    }

    public struct TriadGameResultChance
    {
        public float winChance;
        public float drawChance;
        public ETriadGameState expectedResult;
        public float compScore;

        public TriadGameResultChance(float winChance, float drawChance)
        {
            this.winChance = winChance;
            this.drawChance = drawChance;

            if (winChance < 0.25f && drawChance < 0.25f)
            {
                compScore = winChance / 10.0f;
                expectedResult = ETriadGameState.BlueLost;
            }
            else if (winChance < drawChance)
            {
                compScore = drawChance;
                expectedResult = ETriadGameState.BlueDraw;
            }
            else
            {
                compScore = winChance + 10.0f;
                expectedResult = ETriadGameState.BlueWins;
            }
        }

        public bool IsBetterThan(TriadGameResultChance other)
        {
            return compScore > other.compScore;
        }
    }

    public class TriadGameSession
    {
        public List<TriadGameModifier> modifiers = new List<TriadGameModifier>();
        public ETriadGameSpecialMod specialRules;
        public TriadGameModifier.EFeature modFeatures = TriadGameModifier.EFeature.None;
        public int solverWorkers = 2000;
        public int currentProgress = 0;
        public string solverName = null;

        public static int[][] cachedNeis = new int[9][];

        public TriadGameData StartGame(TriadDeck deckBlue, TriadDeck deckRed, ETriadGameState state)
        {
            TriadGameData gameData = new TriadGameData
            {
                state = state,
                deckBlue = new TriadDeckInstanceManual(deckBlue),
                deckRed = new TriadDeckInstanceManual(deckRed)
            };
            currentProgress = 0;

            return gameData;
        }

        public void UpdateSpecialRules()
        {
            specialRules = ETriadGameSpecialMod.None;
            modFeatures = TriadGameModifier.EFeature.None;
            foreach (TriadGameModifier mod in modifiers)
            {
                specialRules |= mod.GetSpecialRules();
                modFeatures |= mod.GetFeatures();
            }
        }

        public bool PlaceCard(TriadGameData gameData, int cardIdx, TriadDeckInstance cardDeck, ETriadCardOwner owner, int boardPos)
        {
            bool bResult = false;

            bool bIsAllowedOwner =
                ((owner == ETriadCardOwner.Blue) && (gameData.state == ETriadGameState.InProgressBlue)) ||
                ((owner == ETriadCardOwner.Red) && (gameData.state == ETriadGameState.InProgressRed));

            TriadCard card = cardDeck.GetCard(cardIdx);
            if (bIsAllowedOwner && (boardPos >= 0) && (gameData.board[boardPos] == null) && (card != null))
            {
                gameData.board[boardPos] = new TriadCardInstance(card, owner);
                gameData.numCardsPlaced++;

                if (owner == ETriadCardOwner.Blue)
                {
                    gameData.deckBlue.OnCardPlacedFast(cardIdx);
                    gameData.state = ETriadGameState.InProgressRed;
                }
                else
                {
                    gameData.deckRed.OnCardPlacedFast(cardIdx);
                    gameData.state = ETriadGameState.InProgressBlue;
                }
                bResult = true;

                bool bAllowCombo = false;
                if ((modFeatures & TriadGameModifier.EFeature.CardPlaced) != 0)
                {
                    foreach (TriadGameModifier mod in modifiers)
                    {
                        mod.OnCardPlaced(gameData, boardPos);
                        bAllowCombo = bAllowCombo || mod.AllowsCombo();
                    }
                }

                List<int> comboList = new List<int>();
                int comboCounter = 0;
                CheckCaptures(gameData, boardPos, comboList, comboCounter);

                while (bAllowCombo && comboList.Count > 0)
                {
                    if (gameData.bDebugRules) { Logger.WriteLine(">> combo step: {0}", string.Join(",", comboList)); }

                    List<int> nextCombo = new List<int>();
                    comboCounter++;
                    foreach (int pos in comboList)
                    {
                        CheckCaptures(gameData, pos, nextCombo, comboCounter);
                    }

                    comboList = nextCombo;
                }

                if ((modFeatures & TriadGameModifier.EFeature.PostCapture) != 0)
                {
                    foreach (TriadGameModifier mod in modifiers)
                    {
                        mod.OnPostCaptures(gameData, boardPos);
                    }
                }

                if (gameData.numCardsPlaced == gameData.board.Length)
                {
                    OnAllCardsPlaced(gameData);
                }
            }

            return bResult;
        }

        public bool PlaceCard(TriadGameData gameData, TriadCard card, ETriadCardOwner owner, int boardPos)
        {
            TriadDeckInstance useDeck = (owner == ETriadCardOwner.Blue) ? gameData.deckBlue : gameData.deckRed;
            int cardIdx = useDeck.GetCardIndex(card);

            return PlaceCard(gameData, cardIdx, useDeck, owner, boardPos);
        }

        public static int GetBoardPos(int x, int y)
        {
            return (x + (y * TriadGameData.boardSize));
        }

        public static void GetBoardXY(int pos, out int x, out int y)
        {
            x = pos % TriadGameData.boardSize;
            y = pos / TriadGameData.boardSize;
        }

        public static int[] GetNeighbors(TriadGameData gameData, int boardPos)
        {
            int boardPosX = 0;
            int boardPosY = 0;
            GetBoardXY(boardPos, out boardPosX, out boardPosY);

            int[] resultNeis = new int[4];
            resultNeis[(int)ETriadGameSide.Up] = (boardPosY > 0) ? GetBoardPos(boardPosX, boardPosY - 1) : -1;
            resultNeis[(int)ETriadGameSide.Down] = (boardPosY < (TriadGameData.boardSize - 1)) ? GetBoardPos(boardPosX, boardPosY + 1) : -1;
            resultNeis[(int)ETriadGameSide.Right] = (boardPosX > 0) ? GetBoardPos(boardPosX - 1, boardPosY) : -1;
            resultNeis[(int)ETriadGameSide.Left] = (boardPosX < (TriadGameData.boardSize - 1)) ? GetBoardPos(boardPosX + 1, boardPosY) : -1;

            return resultNeis;
        }

        private void CheckCaptures(TriadGameData gameData, int boardPos, List<int> comboList, int comboCounter)
        {
            // combo:
            // - modifiers are active only in intial placement
            // - only card captured via modifiers can initiate combo (same, plus)
            // - type modifiers (ascention, descention) values are baked in card and influence combo
            // - can't proc another plus/same as a result of combo

            int[] neis = cachedNeis[boardPos];
            bool allowMods = comboCounter == 0;
            if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureNei) != 0)
            {
                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnCheckCaptureNeis(gameData, boardPos, neis, comboList);
                }
            }

            TriadCardInstance checkCard = gameData.board[boardPos];
            for (int sideIdx = 0; sideIdx < 4; sideIdx++)
            {
                int neiPos = neis[sideIdx];
                if (neiPos >= 0 && gameData.board[neiPos] != null)
                {
                    TriadCardInstance neiCard = gameData.board[neiPos];
                    if (checkCard.owner != neiCard.owner)
                    {
                        int numPos = checkCard.GetNumber((ETriadGameSide)sideIdx);
                        int numOther = neiCard.GetOppositeNumber((ETriadGameSide)sideIdx);

                        if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureWeights) != 0)
                        {
                            foreach (TriadGameModifier mod in modifiers)
                            {
                                mod.OnCheckCaptureCardWeights(gameData, boardPos, neiPos, ref numPos, ref numOther);
                            }
                        }

                        bool bIsCaptured = (numPos > numOther);
                        if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureMath) != 0)
                        {
                            foreach (TriadGameModifier mod in modifiers)
                            {
                                mod.OnCheckCaptureCardMath(gameData, boardPos, neiPos, numPos, numOther, ref bIsCaptured);
                            }
                        }

                        if (bIsCaptured)
                        {
                            neiCard.owner = checkCard.owner;
                            if (comboCounter > 0)
                            {
                                comboList.Add(neiPos);
                            }

                            if (gameData.bDebugRules)
                            {
                                Logger.WriteLine(">> " + (comboCounter > 0 ? "combo!" : "") + " [" + neiPos + "] " + neiCard.card.Name.GetCodeName() + " => " + neiCard.owner);
                            }
                        }
                    }
                }
            }
        }

        private void OnAllCardsPlaced(TriadGameData gameData)
        {
            int numBlue = (gameData.deckBlue.availableCardMask != 0) ? 1 : 0;
            foreach (TriadCardInstance card in gameData.board)
            {
                if (card.owner == ETriadCardOwner.Blue)
                {
                    numBlue++;
                }
            }

            int numBlueToWin = (gameData.board.Length / 2) + 1;
            gameData.state = (numBlue > numBlueToWin) ? ETriadGameState.BlueWins :
                (numBlue == numBlueToWin) ? ETriadGameState.BlueDraw :
                ETriadGameState.BlueLost;

            if (gameData.bDebugRules)
            {
                TriadCard availBlueCard = gameData.deckBlue.GetFirstAvailableCard();
                Logger.WriteLine(">> blue:" + numBlue + " (in deck:" + ((availBlueCard != null) ? availBlueCard.Name.GetCodeName() : "none") + "), required:" + numBlueToWin + " => " + gameData.state);
            }

            if ((modFeatures & TriadGameModifier.EFeature.AllPlaced) != 0)
            {
                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnAllCardsPlaced(gameData);
                }
            }
        }

        public bool SolverPlayRandomTurn(TriadGameData gameData, Random random)
        {
            const int boardPosMax = TriadGameData.boardSize * TriadGameData.boardSize;
            int boardPos = -1;
            if (gameData.numCardsPlaced < boardPosMax)
            {
                int testPos = random.Next(boardPosMax);
                for (int passIdx = 0; passIdx < boardPosMax; passIdx++)
                {
                    testPos = (testPos + 1) % boardPosMax;
                    if (gameData.board[testPos] == null)
                    {
                        boardPos = testPos;
                        break;
                    }
                }
            }

            int cardIdx = -1;
            TriadDeckInstance useDeck = (gameData.state == ETriadGameState.InProgressBlue) ? gameData.deckBlue : gameData.deckRed;
            if (useDeck.availableCardMask > 0)
            {
                int testIdx = random.Next(TriadDeckInstance.maxAvailableCards);
                for (int passIdx = 0; passIdx < TriadDeckInstance.maxAvailableCards; passIdx++)
                {
                    testIdx = (testIdx + 1) % TriadDeckInstance.maxAvailableCards;
                    if ((useDeck.availableCardMask & (1 << testIdx)) != 0)
                    {
                        cardIdx = testIdx;
                        break;
                    }
                }
            }

            bool bResult = false;
            if (cardIdx >= 0)
            {
                bResult = PlaceCard(gameData, cardIdx, useDeck, (gameData.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red, boardPos);
            }

            return bResult;
        }

        public ETriadGameState SolverPlayRandomGame(TriadGameData gameData, Random random)
        {
            while (SolverPlayRandomTurn(gameData, random)) { }
            return gameData.state;
        }

        private TriadGameResultChance SolverFindWinningProbability(TriadGameData gameData)
        {
            int numWinningWorkers = 0;
            int numDrawingWorkers = 0;

            Parallel.For(0, solverWorkers, workerIdx =>
            //for (int workerIdx = 0; workerIdx < solverWorkers; workerIdx++)
            {
                TriadGameData gameDataCopy = new TriadGameData(gameData);
                Random randomGen = new Random(workerIdx);
                ETriadGameState gameResult = SolverPlayRandomGame(gameDataCopy, randomGen);

                if (gameResult == ETriadGameState.BlueWins)
                {
                    Interlocked.Add(ref numWinningWorkers, 1);
                }
                else if (gameResult == ETriadGameState.BlueDraw)
                {
                    Interlocked.Add(ref numDrawingWorkers, 1);
                }
            });

            return new TriadGameResultChance((float)numWinningWorkers / (float)solverWorkers, (float)numDrawingWorkers / (float)solverWorkers);
        }

        public bool SolverFindBestMove(TriadGameData gameData, out int boardPos, out TriadCard card, out TriadGameResultChance probabilities)
        {
            bool bResult = false;
            card = null;
            boardPos = -1;
            currentProgress = 0;

            string namePrefix = string.IsNullOrEmpty(solverName) ? "" : ("[" + solverName + "] ");

            // prepare available board data
            int availBoardMask = 0;
            int numAvailBoard = 0;
            for (int Idx = 0; Idx < gameData.board.Length; Idx++)
            {
                if (gameData.board[Idx] == null)
                {
                    availBoardMask |= (1 << Idx);
                    numAvailBoard++;
                }
            }

            // prepare available cards data
            TriadDeckInstance useDeck = (gameData.state == ETriadGameState.InProgressBlue) ? gameData.deckBlue : gameData.deckRed;
            ETriadCardOwner turnOwner = (gameData.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red;
            int availCardsMask = 0;

            if (gameData.state == ETriadGameState.InProgressBlue && gameData.forcedCardIdx >= 0)
            {
                availCardsMask = 1 << gameData.forcedCardIdx;
            }
            else
            {
                availCardsMask = useDeck.availableCardMask;
            }

            if ((modFeatures & TriadGameModifier.EFeature.FilterNext) != 0)
            {
                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnFilterNextCards(gameData, ref availCardsMask);
                }
            }

            int numAvailCards = 0;
            for (int Idx = 0; Idx < TriadDeckInstance.maxAvailableCards; Idx++)
            {
                numAvailCards += ((availCardsMask & (1 << Idx)) != 0) ? 1 : 0;
            }

            // check all combinations
            if ((numAvailCards > 0) && (numAvailBoard > 0))
            {
                int numCombinations = numAvailCards * numAvailBoard;
                TriadGameResultChance bestProb = new TriadGameResultChance(-1.0f, 0);

                int cardProgressCounter = 0;
                for (int cardIdx = 0; cardIdx < TriadDeckInstance.maxAvailableCards; cardIdx++)
                {
                    bool bCardNotAvailable = (availCardsMask & (1 << cardIdx)) == 0;
                    if (bCardNotAvailable)
                    {
                        continue;
                    }

                    currentProgress = 100 * cardProgressCounter / numAvailCards;
                    cardProgressCounter++;

                    for (int boardIdx = 0; boardIdx < gameData.board.Length; boardIdx++)
                    {
                        bool bBoardNotAvailable = (availBoardMask & (1 << boardIdx)) == 0;
                        if (bBoardNotAvailable)
                        {
                            continue;
                        }

                        TriadGameData gameDataCopy = new TriadGameData(gameData);
                        bool bPlaced = PlaceCard(gameDataCopy, cardIdx, useDeck, turnOwner, boardIdx);
                        if (bPlaced)
                        {
                            TriadGameResultChance gameProb = SolverFindWinningProbability(gameDataCopy);
                            if (gameProb.IsBetterThan(bestProb))
                            {
                                bestProb = gameProb;
                                card = useDeck.GetCard(cardIdx);
                                boardPos = boardIdx;
                                bResult = true;
                            }
                        }
                    }
                }

                probabilities = bestProb;
                Logger.WriteLine(namePrefix + "Solver win:" + bestProb.winChance.ToString("P2") + " (draw:" + bestProb.drawChance.ToString("P2") +
                    "), blue[" + gameData.deckBlue + "], red[" + gameData.deckRed + "], turn:" + turnOwner + ", availBoard:" + numAvailBoard +
                    " (" + availBoardMask.ToString("x") + ") availCards:" + numAvailCards + " (" + (useDeck == gameData.deckBlue ? "B" : "R") + ":" + availCardsMask.ToString("x") + ")");
            }
            else
            {
                probabilities = new TriadGameResultChance(0, 0);
                Logger.WriteLine(namePrefix + "Can't find move! availSpots:" + numAvailBoard + " (" + availBoardMask.ToString("x") +
                    ") availCards:" + numAvailCards + " (" + (useDeck == gameData.deckBlue ? "B" : "R") + ":" + availCardsMask.ToString("x") + ")");
            }

            return bResult;
        }

        public static void StaticInitialize()
        {
            for (int idxPos = 0; idxPos < 9; idxPos++)
            {
                cachedNeis[idxPos] = GetNeighbors(null, idxPos);
            }
        }

        public static void RunSolverStressTest()
        {
            int numIterations = 1000 * 1000;
            Logger.WriteLine("Solver testing start, numIterations:" + numIterations);

            Stopwatch timer = new Stopwatch();
            timer.Start();

            TriadDeck testDeck = new TriadDeck(new int[] { 10, 20, 30, 40, 50 });
            TriadNpc testNpc = TriadNpcDB.Get().Find("Garima");
            Random randStream = new Random();

            TriadGameSession solver = new TriadGameSession();
            solver.modifiers.AddRange(testNpc.Rules);
            solver.UpdateSpecialRules();

            for (int Idx = 0; Idx < numIterations; Idx++)
            {
                TriadGameData testData = solver.StartGame(testDeck, testNpc.Deck, ETriadGameState.InProgressBlue);
                Random sessionRand = new Random(randStream.Next());
                solver.SolverPlayRandomGame(testData, sessionRand);
            }

            timer.Stop();
            Logger.WriteLine("Solver testing finished, time taken:" + timer.ElapsedMilliseconds + "ms");

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

        #region Validation

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

            public bool VerifyState(TriadGameData gameState)
            {
                if (expectedState != null)
                {
                    for (int idx = 0; idx < expectedState.Length; idx++)
                    {
                        if (gameState.board[idx].owner != expectedState[idx])
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
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public static void RunTest(string configPath)
        {
            string testName = System.IO.Path.GetFileNameWithoutExtension(configPath);

            string configText = System.IO.File.ReadAllText(configPath);
            JsonParser.ObjectValue configOb = JsonParser.ParseJson(configText);
            if (configOb["type"] != "Solver")
            {
                return;
            }

            TriadGameSession testSession = new TriadGameSession();

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

            foreach (string modName in configData.rules)
            {
                testSession.modifiers.Add(mapValidationRules[modName]);
            }

            testSession.UpdateSpecialRules();
            TriadGameData testGameData = new TriadGameData() { bDebugRules = true };

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

                Logger.WriteLine("move[{0}]: [{1}] {2}: {3}", idx, move.boardPos, move.owner, move.card);

                bool result = testSession.PlaceCard(testGameData, move.card, move.owner, move.boardPos);
                if (!result)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Can't place card!", testName);
                    throw new Exception(exceptionMsg);
                }

                result = move.VerifyState(testGameData);
                if (!result)
                {
                    string exceptionMsg = string.Format("Test {0} failed! Finished with bad state!", testName);
                    throw new Exception(exceptionMsg);
                }
            }
        }

        #endregion
    }
}
