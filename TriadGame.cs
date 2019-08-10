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

        public static int boardSize = 3;

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
        public int solverWorkers = 2000;
        public int currentProgress = 0;
        public TriadCard forcedBlueCard = null;

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
            foreach (TriadGameModifier mod in modifiers)
            {
                specialRules |= mod.GetSpecialRules();
            }
        }

        public bool PlaceCard(TriadGameData gameData, TriadCard card, ETriadCardOwner owner, int boardPos)
        {
            bool bResult = false;

            bool bIsAllowedOwner =
                ((owner == ETriadCardOwner.Blue) && (gameData.state == ETriadGameState.InProgressBlue)) ||
                ((owner == ETriadCardOwner.Red) && (gameData.state == ETriadGameState.InProgressRed));

            if (bIsAllowedOwner && (boardPos >= 0) && (gameData.board[boardPos] == null))
            {
                gameData.board[boardPos] = new TriadCardInstance(card, owner);
                gameData.numCardsPlaced++;
                
                if (owner == ETriadCardOwner.Blue)
                {
                    gameData.deckBlue.OnCardPlaced(card);
                    gameData.state = ETriadGameState.InProgressRed;
                }
                else
                {
                    gameData.deckRed.OnCardPlaced(card);
                    gameData.state = ETriadGameState.InProgressBlue;
                }

                bResult = true;

                bool bAllowCombo = false;
                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnCardPlaced(gameData, boardPos);
                    bAllowCombo = bAllowCombo || mod.AllowsCombo();
                }

                List<int> comboList = new List<int>();
                int comboCounter = 0;
                CheckCaptures(gameData, boardPos, comboList, comboCounter);

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

                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnPostCaptures(gameData, boardPos);
                }

                CheckGameState(gameData);
            }

            return bResult;
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
            if (comboCounter == 0)
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
                        foreach (TriadGameModifier mod in modifiers)
                        {
                            mod.OnCheckCaptureCardWeights(gameData, boardPos, neiPos, ref numPos, ref numOther);
                        }

                        bool bIsCaptured = (numPos > numOther);
                        foreach (TriadGameModifier mod in modifiers)
                        {
                            mod.OnCheckCaptureCardMath(gameData, boardPos, neiPos, numPos, numOther, ref bIsCaptured);
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

        private void CheckGameState(TriadGameData gameData)
        {
            if (gameData.numCardsPlaced == gameData.board.Length)
            {
                TriadCard availBlueCard = gameData.deckBlue.GetFirstAvailableCard();
                int numBlue = (availBlueCard != null) ? 1 : 0;
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
                    Logger.WriteLine(">> blue:" + numBlue + " (in deck:" + ((availBlueCard != null) ? availBlueCard.Name : "none") + "), required:" + numBlueToWin + " => " + gameData.state);
                }

                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnAllCardsPlaced(gameData);
                }
            }
        }

        private int[] GetAllowedBoardPositions(TriadGameData gameData)
        {
            int[] result = null;

            int numSpotsLeft = gameData.board.Length - gameData.numCardsPlaced;
            if (numSpotsLeft > 0)
            {
                result = new int[numSpotsLeft];
                int resultIdx = 0;
                for (int Idx = 0; Idx < gameData.board.Length; Idx++)
                {
                    if (gameData.board[Idx] == null)
                    {
                        result[resultIdx] = Idx;
                        resultIdx++;
                    }
                }
            }

            return result;
        }

        public bool SolverPlayRandomTurn(TriadGameData gameData, Random random)
        {
            int[] availSpots = GetAllowedBoardPositions(gameData);
            int boardPos = (availSpots != null) ? availSpots[random.Next(availSpots.Length)] : -1;

            TriadCard[] availCards = (gameData.state == ETriadGameState.InProgressBlue) ? gameData.deckBlue.GetAvailableCards() : gameData.deckRed.GetAvailableCards();
            TriadCard cardToPlay = (availCards != null && availCards.Length > 0) ? availCards[random.Next(availCards.Length)] : null;

            bool bResult = false;
            if (cardToPlay != null)
            {
                bResult = PlaceCard(gameData, cardToPlay, (gameData.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red, boardPos);
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

            int[] availSpots = GetAllowedBoardPositions(gameData);
            TriadCard[] availCards = null;
            if (gameData.state == ETriadGameState.InProgressBlue && forcedBlueCard != null)
            {
                availCards = new TriadCard[1] { forcedBlueCard };
            }
            else
            {
                availCards = (gameData.state == ETriadGameState.InProgressBlue) ? gameData.deckBlue.GetAvailableCards() : gameData.deckRed.GetAvailableCards();
            }

            foreach (TriadGameModifier mod in modifiers)
            {
                mod.OnFilterNextCards(gameData, ref availCards);
            }

            int numCombinations = 0;
            if ((availSpots != null) && (availCards != null))
            {
                numCombinations = availCards.Length * availCards.Length;
                TriadGameResultChance bestProb = new TriadGameResultChance(-1.0f, 0);

                for (int Idx = 0; Idx < availCards.Length; Idx++)
                {
                    TriadCard testCard = availCards[Idx];
                    currentProgress = 100 * Idx / availCards.Length;

                    foreach (int testPos in availSpots)
                    {
                        TriadGameData gameDataCopy = new TriadGameData(gameData);
                        bool bPlaced = PlaceCard(gameDataCopy, testCard, (gameDataCopy.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red, testPos);
                        if (bPlaced)
                        {
                            TriadGameResultChance gameProb = SolverFindWinningProbability(gameDataCopy);
                            if (gameProb.IsBetterThan(bestProb))
                            {
                                bestProb = gameProb;
                                card = testCard;
                                boardPos = testPos;
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
                Logger.WriteLine("Can't find move!" +
                    " availSpots:" + ((availSpots != null) ? availSpots.Length : 0) +
                    ", availCards:" + ((availCards != null) ? availCards.Length : 0));
            }

            return bResult;
        }
    }
}
