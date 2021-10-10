using MgAl2O4.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public abstract class TriadGameAgent
    {
        public virtual void Initialize(TriadGameSolver solver, int sessionSeed) { }
        public virtual float GetProgress() { return 0.0f; }

        public abstract bool FindNextMove(TriadGameSolver solver, TriadGameSimulationState gameState, out int cardIdx, out int boardPos, out SolverResult solverResult, bool debugMode = false);
    }

    public class TriadGameAgentRandom : TriadGameAgent
    {
        private Random randGen;

        public TriadGameAgentRandom() { }
        public TriadGameAgentRandom(TriadGameSolver solver, int sessionSeed)
        {
            Initialize(solver, sessionSeed);
        }

        public override void Initialize(TriadGameSolver solver, int sessionSeed)
        {
            randGen = new Random(sessionSeed);
        }

        public override bool FindNextMove(TriadGameSolver solver, TriadGameSimulationState gameState, out int cardIdx, out int boardPos, out SolverResult solverResult, bool debugMode = false)
        {
            const int boardPosMax = TriadGameSimulationState.boardSize * TriadGameSimulationState.boardSize;

            boardPos = -1;
            if (gameState.numCardsPlaced < boardPosMax)
            {
                int testPos = randGen.Next(boardPosMax);
                for (int passIdx = 0; passIdx < boardPosMax; passIdx++)
                {
                    testPos = (testPos + 1) % boardPosMax;
                    if (gameState.board[testPos] == null)
                    {
                        boardPos = testPos;
                        break;
                    }
                }
            }

            cardIdx = -1;
            TriadDeckInstance useDeck = (gameState.state == ETriadGameState.InProgressBlue) ? gameState.deckBlue : gameState.deckRed;
            if (useDeck.availableCardMask > 0)
            {
                int testIdx = randGen.Next(TriadDeckInstance.maxAvailableCards);
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

            solverResult = SolverResult.Zero;
            return (boardPos >= 0) && (cardIdx >= 0);
        }
    }

    public class TriadGameAgentDerpyCarlo : TriadGameAgent
    {
        private const int NumWorkers = 2000;
        private float currentProgress = 0;

        public override float GetProgress()
        {
            return currentProgress;
        }

        public override bool FindNextMove(TriadGameSolver solver, TriadGameSimulationState gameState, out int cardIdx, out int boardPos, out SolverResult solverResult, bool debugMode = false)
        {
            cardIdx = -1;
            boardPos = -1;
            solverResult = SolverResult.Zero;
            currentProgress = 0.0f;

            solver.FindAvailableActions(gameState, out int availBoardMask, out int numAvailBoard, out int availCardsMask, out int numAvailCards);
            if (numAvailCards > 0 && numAvailBoard > 0)
            {
                var useDeck = (gameState.state == ETriadGameState.InProgressBlue) ? gameState.deckBlue : gameState.deckRed;
                var turnOwner = (gameState.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red;
                int cardProgressCounter = 0;

                for (int testCardIdx = 0; testCardIdx < TriadDeckInstance.maxAvailableCards; testCardIdx++)
                {
                    bool cardNotAvailable = (availCardsMask & (1 << testCardIdx)) == 0;
                    if (cardNotAvailable)
                    {
                        continue;
                    }

                    currentProgress = 1.0f * cardProgressCounter / numAvailCards;
                    cardProgressCounter++;

                    for (int boardIdx = 0; boardIdx < gameState.board.Length; boardIdx++)
                    {
                        bool boardNotAvailable = (availBoardMask & (1 << boardIdx)) == 0;
                        if (boardNotAvailable)
                        {
                            continue;
                        }

                        var gameStateCopy = new TriadGameSimulationState(gameState);
                        bool isPlaced = solver.simulation.PlaceCard(gameStateCopy, testCardIdx, useDeck, turnOwner, boardIdx);
                        if (isPlaced)
                        {
                            var branchResult = FindWinningProbability(solver, gameStateCopy);
                            if (branchResult.IsBetterThan(solverResult))
                            {
                                solverResult = branchResult;
                                cardIdx = testCardIdx;
                                boardPos = boardIdx;
                            }
                        }
                    }
                }

                if (debugMode)
                {
                    string namePrefix = string.IsNullOrEmpty(solver.name) ? "" : ("[" + solver.name + "] ");
                    Logger.WriteLine("{0}Solver win:{1:P2} (draw:{2:P2}), blue[{3}], red[{4}], turn:{5}, availBoard:{6} ({7:x}), availCards:{8} ({9}:{10:x})",
                        namePrefix,
                        solverResult.winChance, solverResult.drawChance,
                        gameState.deckBlue, gameState.deckRed, turnOwner,
                        numAvailBoard, availBoardMask,
                        numAvailCards, gameState.state == ETriadGameState.InProgressBlue ? "B" : "R", availCardsMask);
                }
            }
            else
            {
                if (debugMode)
                {
                    string namePrefix = string.IsNullOrEmpty(solver.name) ? "" : ("[" + solver.name + "] ");
                    Logger.WriteLine("{0}Can't find move! availBoard:{1} ({2:x}), availCards:{3} ({4}:{5:x})",
                        namePrefix,
                        numAvailBoard, availBoardMask,
                        numAvailCards, gameState.state == ETriadGameState.InProgressBlue ? "B" : "R", availCardsMask);
                }
            }

            return (boardPos >= 0) && (cardIdx >= 0);
        }

        private SolverResult FindWinningProbability(TriadGameSolver solver, TriadGameSimulationState gameState)
        {
            int numWinningWorkers = 0;
            int numDrawingWorkers = 0;

            Parallel.For(0, NumWorkers, workerIdx =>
            //for (int workerIdx = 0; workerIdx < solverWorkers; workerIdx++)
            {
                var gameStateCopy = new TriadGameSimulationState(gameState);
                var agentRandom = new TriadGameAgentRandom(solver, workerIdx);

                solver.RunSimulation(gameStateCopy, agentRandom, agentRandom);

                if (gameStateCopy.state == ETriadGameState.BlueWins)
                {
                    Interlocked.Add(ref numWinningWorkers, 1);
                }
                else if (gameStateCopy.state == ETriadGameState.BlueDraw)
                {
                    Interlocked.Add(ref numDrawingWorkers, 1);
                }
            });

            return new SolverResult((float)numWinningWorkers / (float)NumWorkers, (float)numDrawingWorkers / (float)NumWorkers);
        }
    }
}
