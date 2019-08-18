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
            bDebugRules = false;

            for (int Idx = 0; Idx < typeMods.Length; Idx++)
            {
                typeMods[Idx] = 0;
            }
        }

        public TriadGameData(TriadGameData copyFrom)
        {
            board = new TriadCardInstance[boardSize * boardSize];
            for (int Idx = 0; Idx < board.Length; Idx++)
            {
                if (copyFrom.board[Idx] != null)
                {
                    board[Idx] = new TriadCardInstance(copyFrom.board[Idx]);
                }
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
        public TriadCard forcedBlueCard = null;

#if DEBUG
        private class SolverPerfStats
        {
            public long PlayRandomTurnSelectSpot;
            public long PlayRandomTurnSelectCard;
            public long PlayRandomTurnPlaceCard;
            public long PlaceCardOnPlaced;
            public long PlaceCardOnPlacedMods;
            public long PlaceCardCaptures;
            public long PlaceCardCapturesCombo;
            public long PlaceCardPostCaptures;
            public long PlaceCardAllPlaced;
        }
        private static SolverPerfStats perfStats = new SolverPerfStats();
#endif // DEBUG

        public TriadGameData StartGame(TriadDeck deckBlue, TriadDeck deckRed, ETriadGameState state)
        {
            TriadGameData gameData = new TriadGameData
            {
                state = state,
                deckBlue = new TriadDeckInstanceManual(deckBlue),
                deckRed = new TriadDeckInstanceManual(deckRed)
            };
            currentProgress = 0;
            forcedBlueCard = null;

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
#if DEBUG
                Stopwatch timer = new Stopwatch();
                timer.Start();
#endif // DEBUG

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
#if DEBUG
                perfStats.PlaceCardOnPlaced += timer.ElapsedTicks;
                timer.Restart();
#endif // DEBUG
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
#if DEBUG
                perfStats.PlaceCardOnPlacedMods += timer.ElapsedTicks;
                timer.Restart();
#endif // DEBUG

                List<int> comboList = new List<int>();
                int comboCounter = 0;
                CheckCaptures(gameData, boardPos, comboList, comboCounter);
#if DEBUG
                perfStats.PlaceCardCaptures += timer.ElapsedTicks;
                timer.Restart();
#endif // DEBUG

                while (bAllowCombo && comboList.Count > 0)
                {
                    List<int> nextCombo = new List<int>();
                    comboCounter++;
                    foreach (int pos in comboList)
                    {
                        CheckCaptures(gameData, pos, nextCombo, comboCounter);
                    }

                    comboList = nextCombo;
                }
#if DEBUG
                perfStats.PlaceCardCapturesCombo += timer.ElapsedTicks;
                timer.Restart();
#endif // DEBUG

                if ((modFeatures & TriadGameModifier.EFeature.PostCapture) != 0)
                {
                    foreach (TriadGameModifier mod in modifiers)
                    {
                        mod.OnPostCaptures(gameData, boardPos);
                    }
                }
#if DEBUG
                perfStats.PlaceCardPostCaptures += timer.ElapsedTicks;
                timer.Restart();
#endif // DEBUG

                if (gameData.numCardsPlaced == gameData.board.Length)
                {
                    OnAllCardsPlaced(gameData);
                }
#if DEBUG
                timer.Stop();
                perfStats.PlaceCardAllPlaced += timer.ElapsedTicks;
#endif // DEBUG
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

        public int[] GetNeighbors(TriadGameData gameData, int boardPos)
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
            int[] neis = GetNeighbors(gameData, boardPos);
            bool bAllowBasicCaptureCombo = (comboCounter > 0);
            if ((comboCounter == 0) && ((modFeatures & TriadGameModifier.EFeature.CaptureNei) != 0))
            {
                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnCheckCaptureNeis(gameData, boardPos, neis, comboList);
                }

                bAllowBasicCaptureCombo = bAllowBasicCaptureCombo || (comboList.Count > 0);
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
                        if ((modFeatures & TriadGameModifier.EFeature.CaptureWeights) != 0)
                        {
                            foreach (TriadGameModifier mod in modifiers)
                            {
                                mod.OnCheckCaptureCardWeights(gameData, boardPos, neiPos, ref numPos, ref numOther);
                            }
                        }

                        bool bIsCaptured = (numPos > numOther);
                        if ((modFeatures & TriadGameModifier.EFeature.CaptureMath) != 0)
                        {
                            foreach (TriadGameModifier mod in modifiers)
                            {
                                mod.OnCheckCaptureCardMath(gameData, boardPos, neiPos, numPos, numOther, ref bIsCaptured);
                            }
                        }

                        if (bIsCaptured)
                        {
                            neiCard.owner = checkCard.owner;
                            if (bAllowBasicCaptureCombo)
                            {
                                comboList.Add(neiPos);
                            }

                            if (gameData.bDebugRules)
                            {
                                Logger.WriteLine(">> " + (comboCounter > 0 ? "combo!" : "") + " [" + neiPos + "] " + neiCard.card.Name + " => " + neiCard.owner);
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
                Logger.WriteLine(">> blue:" + numBlue + " (in deck:" + ((availBlueCard != null) ? availBlueCard.Name : "none") + "), required:" + numBlueToWin + " => " + gameData.state);
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
#if DEBUG
            Stopwatch timer = new Stopwatch();
            timer.Start();
#endif // DEBUG
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
#if DEBUG
            perfStats.PlayRandomTurnSelectSpot += timer.ElapsedTicks;
            timer.Restart();
#endif // DEBUG

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
#if DEBUG
            perfStats.PlayRandomTurnSelectCard += timer.ElapsedTicks;
            timer.Restart();
#endif // DEBUG

            bool bResult = false;
            if (cardIdx >= 0)
            {
                bResult = PlaceCard(gameData, cardIdx, useDeck, (gameData.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red, boardPos);
            }

#if DEBUG
            perfStats.PlayRandomTurnPlaceCard += timer.ElapsedTicks;
            timer.Stop();
#endif // DEBUG
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
            int availCardsMask = 0;

            if (gameData.state == ETriadGameState.InProgressBlue && forcedBlueCard != null)
            {
                int forcedBlueCardIdx = useDeck.GetCardIndex(forcedBlueCard);
                availCardsMask = 1 << forcedBlueCardIdx;
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
                        bool bPlaced = PlaceCard(gameDataCopy, cardIdx, useDeck, (gameDataCopy.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red, boardIdx);
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
                Logger.WriteLine("Solver win:" + bestProb.winChance.ToString("P2") + " (draw:" + bestProb.drawChance.ToString("P2") + "), blue:" + gameData.deckBlue + ", red:" + gameData.deckRed);
            }
            else
            {
                probabilities = new TriadGameResultChance(0, 0);
                Logger.WriteLine("Can't find move!" + " availSpots:" + numAvailBoard + ", availCards:" + numAvailCards);
            }

            return bResult;
        }

        public static void RunSolverStressTest()
        {
#if DEBUG
            int numIterations = 1000 * 100;
            Logger.WriteLine("Solver testing start, numIterations:" + numIterations);
            perfStats = new SolverPerfStats();

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

            float TicksToMs = 1000.0f / Stopwatch.Frequency;
            Logger.WriteLine(">> PlayRandomTurn.SelectSpot: " + (perfStats.PlayRandomTurnSelectSpot * TicksToMs) + "ms");
            Logger.WriteLine(">> PlayRandomTurn.SelectCard: " + (perfStats.PlayRandomTurnSelectCard * TicksToMs) + "ms");
            Logger.WriteLine(">> PlayRandomTurn.PlaceCard: " + (perfStats.PlayRandomTurnPlaceCard * TicksToMs) + "ms");
            Logger.WriteLine("  >> PlaceCard.OnPlaced: " + (perfStats.PlaceCardOnPlaced * TicksToMs) + "ms");
            Logger.WriteLine("  >> PlaceCard.OnPlacedMods: " + (perfStats.PlaceCardOnPlacedMods * TicksToMs) + "ms");
            Logger.WriteLine("  >> PlaceCard.Captures: " + (perfStats.PlaceCardCaptures * TicksToMs) + "ms");
            Logger.WriteLine("  >> PlaceCard.CapturesCombo: " + (perfStats.PlaceCardCapturesCombo * TicksToMs) + "ms");
            Logger.WriteLine("  >> PlaceCard.PostCaptures: " + (perfStats.PlaceCardPostCaptures * TicksToMs) + "ms");
            Logger.WriteLine("  >> PlaceCard.AllPlaced: " + (perfStats.PlaceCardAllPlaced * TicksToMs) + "ms");
#endif // DEBUG
        }
    }
}
