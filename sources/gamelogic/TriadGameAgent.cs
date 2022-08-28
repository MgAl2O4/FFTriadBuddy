using MgAl2O4.Utils;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public abstract class TriadGameAgent
    {
        [Flags]
        public enum DebugFlags
        {
            None = 0,
            AgentInitialize = 0x1,
            ShowMoveResult = 0x2,
            ShowMoveStart = 0x4,
            ShowMoveDetails = 0x8,
            ShowMoveDetailsRng = 0x10,
        }
        public DebugFlags debugFlags;
        public string agentName = "??";

        public virtual void Initialize(TriadGameSolver solver, int sessionSeed) { }
        public virtual bool IsInitialized() { return true; }
        public virtual float GetProgress() { return 0.0f; }
        public virtual void OnSimulationStart() { }

        public abstract bool FindNextMove(TriadGameSolver solver, TriadGameSimulationState gameState, out int cardIdx, out int boardPos, out SolverResult solverResult);
    }

    /// <summary>
    /// Random pick from all possible actions 
    /// </summary>
    public class TriadGameAgentRandom : TriadGameAgent
    {
        public static bool UseEqualDistribution = false;
        private Random randGen;

        public TriadGameAgentRandom() { }
        public TriadGameAgentRandom(TriadGameSolver solver, int sessionSeed)
        {
            Initialize(solver, sessionSeed);
        }

        public override void Initialize(TriadGameSolver solver, int sessionSeed)
        {
            randGen = new Random(sessionSeed);
            agentName = "Random";
        }

        public override bool IsInitialized()
        {
            return randGen != null;
        }

        public override bool FindNextMove(TriadGameSolver solver, TriadGameSimulationState gameState, out int cardIdx, out int boardPos, out SolverResult solverResult)
        {
#if DEBUG
            if ((debugFlags & DebugFlags.ShowMoveStart) != DebugFlags.None) { Logger.WriteLine($"FindNextMove, numPlaced:{gameState.numCardsPlaced}"); }
#endif // DEBUG

            cardIdx = -1;
            boardPos = -1;
            solverResult = SolverResult.Zero;

            if (!IsInitialized())
            {
                return false;
            }

            if (UseEqualDistribution)
            {
                // proper solution, but ends up lowering initial win chance by A LOT

                solver.FindAvailableActions(gameState, out int availBoardMask, out int numAvailBoard, out int availCardsMask, out int numAvailCards);
                if (numAvailCards > 0 && numAvailBoard > 0)
                {
                    cardIdx = PickBitmaskIndex(availCardsMask, numAvailCards);
                    boardPos = PickBitmaskIndex(availBoardMask, numAvailBoard);
                }
            }
            else
            {
                // OLD IMPLEMENTATION for comparison
                // doesn't guarantee equal distribution = opponent simulation is biased => reported win chance is too high
                // stays for now until i can make CarloScored usable
                //
                const int boardPosMax = TriadGameSimulationState.boardSizeSq;
                if (gameState.numCardsPlaced < TriadGameSimulationState.boardSizeSq)
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
            }

            return (boardPos >= 0) && (cardIdx >= 0);
        }

        protected int PickBitmaskIndex(int mask, int numSet)
        {
            int stepIdx = randGen.Next(numSet);
            return PickRandomBitFromMask(mask, stepIdx);
        }

        public static int PickRandomBitFromMask(int mask, int randStep)
        {
            int bitIdx = 0;
            int testMask = 1 << bitIdx;
            while (testMask <= mask)
            {
                if ((testMask & mask) != 0)
                {
                    randStep--;
                    if (randStep < 0)
                    {
                        return bitIdx;
                    }
                }

                bitIdx++;
                testMask <<= 1;
            }

#if DEBUG
            // more bits set than mask allows?
            Debugger.Break();
#endif
            return -1;
        }
    }

    /// <summary>
    /// Base class for agents recursively exploring action graph
    /// </summary>
    public abstract class TriadGameAgentGraphExplorer : TriadGameAgent
    {
        protected float currentProgress = 0;
        protected int sessionSeed = 0;
        private Random failsafeRandStream = null;

        public override float GetProgress()
        {
            return currentProgress;
        }

        public override void Initialize(TriadGameSolver solver, int sessionSeed)
        {
            this.sessionSeed = sessionSeed;
        }

        public override bool FindNextMove(TriadGameSolver solver, TriadGameSimulationState gameState, out int cardIdx, out int boardPos, out SolverResult solverResult)
        {
#if DEBUG
            if ((debugFlags & DebugFlags.ShowMoveStart) != DebugFlags.None) { Logger.WriteLine($"FindNextMove, numPlaced:{gameState.numCardsPlaced}"); }
#endif // DEBUG

            cardIdx = -1;
            boardPos = -1;

            bool isFinished = IsFinished(gameState, out solverResult);
            if (!isFinished && IsInitialized())
            {
                _ = SearchActionSpace(solver, gameState, 0, out cardIdx, out boardPos, out solverResult);
            }

            return (cardIdx >= 0) && (boardPos >= 0);
        }

        protected bool IsFinished(TriadGameSimulationState gameState, out SolverResult gameResult)
        {
            // end game conditions, owner always fixed as blue
            switch (gameState.state)
            {
                case ETriadGameState.BlueWins:
                    gameResult = new SolverResult(1, 0, 1);
                    return true;

                case ETriadGameState.BlueDraw:
                    gameResult = new SolverResult(0, 1, 1);
                    return true;

                case ETriadGameState.BlueLost:
                    gameResult = new SolverResult(0, 0, 1);
                    return true;

                default: break;
            }

            gameResult = SolverResult.Zero;
            return false;
        }

        protected virtual SolverResult SearchActionSpace(TriadGameSolver solver, TriadGameSimulationState gameState, int searchLevel, out int bestCardIdx, out int bestBoardPos, out SolverResult bestActionResult)
        {
            // don't check finish condition at start! 
            // this is done before caling this function (from FindNextMove / recursive), so it doesn't have to be duplicated in every derrived class

            bestCardIdx = -1;
            bestBoardPos = -1;
            bestActionResult = SolverResult.Zero;

            // game in progress, explore actions
            bool isRootLevel = searchLevel == 0;
            if (isRootLevel)
            {
                currentProgress = 0.0f;
            }

            float numWinsTotal = 0;
            float numDrawsTotal = 0;
            long numGamesTotal = 0;

            solver.FindAvailableActions(gameState, out int availBoardMask, out int numAvailBoard, out int availCardsMask, out int numAvailCards);
            if (numAvailCards > 0 && numAvailBoard > 0)
            {
                var turnOwner = (gameState.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red;
                int cardProgressCounter = 0;
                bool hasValidPlacements = false;

                for (int cardIdx = 0; cardIdx < TriadDeckInstance.maxAvailableCards; cardIdx++)
                {
                    bool cardNotAvailable = (availCardsMask & (1 << cardIdx)) == 0;
                    if (cardNotAvailable)
                    {
                        continue;
                    }

                    if (isRootLevel)
                    {
                        currentProgress = 1.0f * cardProgressCounter / numAvailCards;
                        cardProgressCounter++;
                    }

                    for (int boardIdx = 0; boardIdx < gameState.board.Length; boardIdx++)
                    {
                        bool boardNotAvailable = (availBoardMask & (1 << boardIdx)) == 0;
                        if (boardNotAvailable)
                        {
                            continue;
                        }

                        var gameStateCopy = new TriadGameSimulationState(gameState);
                        var useDeck = (gameStateCopy.state == ETriadGameState.InProgressBlue) ? gameStateCopy.deckBlue : gameStateCopy.deckRed;

                        bool isPlaced = solver.simulation.PlaceCard(gameStateCopy, cardIdx, useDeck, turnOwner, boardIdx);
                        if (isPlaced)
                        {
                            // check if finished before going deeper
                            bool isFinished = IsFinished(gameStateCopy, out var branchResult);
                            if (!isFinished)
                            {
                                gameStateCopy.forcedCardIdx = -1;
                                branchResult = SearchActionSpace(solver, gameStateCopy, searchLevel + 1, out _, out _, out _);
                            }

#if DEBUG
                            if ((debugFlags & DebugFlags.ShowMoveDetails) != DebugFlags.None && isRootLevel) { Logger.WriteLine($"  board[{boardIdx}], card[{cardIdx}] = {branchResult}"); }
#endif // DEBUG
                            if (branchResult.IsBetterThan(bestActionResult) || !hasValidPlacements)
                            {
                                bestActionResult = branchResult;
                                bestCardIdx = cardIdx;
                                bestBoardPos = boardIdx;
                            }

                            numWinsTotal += branchResult.numWins;
                            numDrawsTotal += branchResult.numDraws;
                            numGamesTotal += branchResult.numGames;
                            hasValidPlacements = true;
                        }
                    }
                }

                if (!hasValidPlacements)
                {
                    // failsafe in case simulation runs into any issues
                    if (failsafeRandStream == null)
                    {
                        failsafeRandStream = new Random(sessionSeed);
                    }

                    bestCardIdx = TriadGameAgentRandom.PickRandomBitFromMask(availCardsMask, failsafeRandStream.Next(numAvailCards));
                    bestBoardPos = TriadGameAgentRandom.PickRandomBitFromMask(availBoardMask, failsafeRandStream.Next(numAvailBoard));
                }

#if DEBUG
                if ((debugFlags & DebugFlags.ShowMoveResult) != DebugFlags.None && isRootLevel)
                {
                    string namePrefix = string.IsNullOrEmpty(solver.name) ? "" : ("[" + solver.name + "] ");
                    Logger.WriteLine("{0}Solver {11}win:{1:P2} (draw:{2:P2}), blue[{3}], red[{4}], turn:{5}, availBoard:{6} ({7:x}), availCards:{8} ({9}:{10:x})",
                        namePrefix,
                        bestActionResult.winChance, bestActionResult.drawChance,
                        gameState.deckBlue, gameState.deckRed, turnOwner,
                        numAvailBoard, availBoardMask,
                        numAvailCards, gameState.state == ETriadGameState.InProgressBlue ? "B" : "R", availCardsMask,
                        hasValidPlacements ? "[FAILSAFE] " : "");
                }
#endif // DEBUG
            }
            else
            {
#if DEBUG
                if ((debugFlags & DebugFlags.ShowMoveResult) != DebugFlags.None && isRootLevel)
                {
                    string namePrefix = string.IsNullOrEmpty(solver.name) ? "" : ("[" + solver.name + "] ");
                    Logger.WriteLine("{0}Can't find move! availBoard:{1} ({2:x}), availCards:{3} ({4}:{5:x})",
                        namePrefix,
                        numAvailBoard, availBoardMask,
                        numAvailCards, gameState.state == ETriadGameState.InProgressBlue ? "B" : "R", availCardsMask);
                }
#endif // DEBUG
            }

            // what to do with results depend on current move owner:
            //   Agent's player (search levels: 0, 2, 4, ...)
            //   - result of processing this level is MAX(result branch)
            //
            //   opponent player (search levels: 1, 3, ...)
            //   - min/max says MIN, but let's go with AVG instead to be more optimistic
            //   - result of processing this level is AVG, use total counters to create chance data

            bool isOwnerTurn = (searchLevel % 2) == 0;
            return isOwnerTurn ? bestActionResult : new SolverResult(numWinsTotal, numDrawsTotal, numGamesTotal);
        }
    }

    /// <summary>
    /// Single level MCTS, each available action spins 2000 random games and best one is selected 
    /// </summary>
    public class TriadGameAgentDerpyCarlo : TriadGameAgentGraphExplorer
    {
        protected int numWorkers = 2000;
        protected TriadGameAgentRandom[] workerAgents;

        public override void Initialize(TriadGameSolver solver, int sessionSeed)
        {
            base.Initialize(solver, sessionSeed);
            agentName = "DerpyCarlo";

            // initialize all random streams just once, it's enough for seeing and having unique stream for each worker
            workerAgents = new TriadGameAgentRandom[numWorkers];
            for (int idx = 0; idx < numWorkers; idx++)
            {
                workerAgents[idx] = new TriadGameAgentRandom(solver, sessionSeed + idx);
            }
        }

        public override bool IsInitialized()
        {
            return workerAgents != null;
        }

        protected override SolverResult SearchActionSpace(TriadGameSolver solver, TriadGameSimulationState gameState, int searchLevel, out int bestCardIdx, out int bestBoardPos, out SolverResult bestActionResult)
        {
            bool runWorkers = CanRunRandomExploration(solver, gameState, searchLevel);
            if (runWorkers)
            {
                bestCardIdx = -1;
                bestBoardPos = -1;
                bestActionResult = FindWinningProbability(solver, gameState);
#if DEBUG
                if ((debugFlags & DebugFlags.ShowMoveDetailsRng) != DebugFlags.None)
                {
                    Logger.WriteLine($"level:{searchLevel}, numPlaced:{gameState.numCardsPlaced} => random workers:{bestActionResult}");
                }
#endif // DEBUG

                return bestActionResult;
            }

            var result = base.SearchActionSpace(solver, gameState, searchLevel, out bestCardIdx, out bestBoardPos, out bestActionResult);
#if DEBUG
            if ((debugFlags & DebugFlags.ShowMoveDetails) != DebugFlags.None && searchLevel == 0)
            {
                Logger.WriteLine($"level:{searchLevel}, numPlaced:{gameState.numCardsPlaced} => result:{bestActionResult}");
            }
#endif // DEBUG
            return result;
        }

        protected virtual bool CanRunRandomExploration(TriadGameSolver solver, TriadGameSimulationState gameState, int searchLevel)
        {
            return searchLevel > 0;
        }

        protected virtual SolverResult FindWinningProbability(TriadGameSolver solver, TriadGameSimulationState gameState)
        {
            int numWinningWorkers = 0;
            int numDrawingWorkers = 0;

            _ = Parallel.For(0, numWorkers, workerIdx =>
            //for (int workerIdx = 0; workerIdx < solverWorkers; workerIdx++)
            {
                var gameStateCopy = new TriadGameSimulationState(gameState);
                var agent = workerAgents[workerIdx];

                solver.RunSimulation(gameStateCopy, agent, agent);

                if (gameStateCopy.state == ETriadGameState.BlueWins)
                {
                    _ = Interlocked.Add(ref numWinningWorkers, 1);
                }
                else if (gameStateCopy.state == ETriadGameState.BlueDraw)
                {
                    _ = Interlocked.Add(ref numDrawingWorkers, 1);
                }
            });

            // return normalized score so it can be compared 
            return new SolverResult(1.0f * numWinningWorkers / numWorkers, 1.0f * numDrawingWorkers / numWorkers, 1);
        }
    }

    /// <summary>
    /// Switches between derpy MCTS and full exploration depending on size of game space 
    /// </summary>
    public class TriadGameAgentCarloTheExplorer : TriadGameAgentDerpyCarlo
    {
        // 10k seems to be sweet spot
        // - 1k: similar time, lower accuracy
        // - 100k: 8x longer, similar accuracy
        public const long MaxStatesToExplore = 10 * 1000;

        private int minPlacedToExplore = 10;
        private int minPlacedToExploreWithForced = 10;

        public override void Initialize(TriadGameSolver solver, int sessionSeed)
        {
            base.Initialize(solver, sessionSeed);
            agentName = "CarloTheExplorer";

            // cache number of possible states depending on cards placed
            // 0: (5 * 9) * (5 * 8) * (4 * 7) * (4 * 6) * ...                   = (5 * 5 * 4 * 4 * 3 * 3 * 2 * 2 * 1) * 9! = (5! * 5!) * 9!
            // 1: (5 * 8) * (4 * 7) * (4 * 6) * ...                             = (5 * 4 * 4 * 3 * 3 * 2 * 2 * 1) * 8!     = (4! * 5!) * 8!
            // ...
            // 6: (2 * 3) * (1 * 2) * (1 * 1)
            // 7: (1 * 2) * (1 * 1)
            // 8: (1 * 1)
            // 9: 0
            //
            // num states = num board positions * num cards, 
            // - board(num placed) => x == 0 ? 0 : x!
            // - card(num placed) => forced ? 1 : ((x + 2) / 2)! * ((x + 1) / 2)!

            long numStatesForced = 1;
            long numStates = 1;

            const int maxToPlace = TriadGameSimulationState.boardSizeSq;
            for (int numToPlace = 1; numToPlace <= maxToPlace; numToPlace++)
            {
                int numPlaced = maxToPlace - numToPlace;

                numStatesForced *= numToPlace;
                if (numStatesForced <= MaxStatesToExplore)
                {
                    minPlacedToExploreWithForced = numPlaced;
                }

                numStates *= numToPlace * ((numToPlace + 2) / 2) * ((numToPlace + 1) / 2);
                if (numStates <= MaxStatesToExplore)
                {
                    minPlacedToExplore = numPlaced;
                }
            }

#if DEBUG
            if ((debugFlags & DebugFlags.AgentInitialize) != DebugFlags.None) { Logger.WriteLine($"{agentName}: minPlacedToExplore:{minPlacedToExplore}, minPlacedToExploreWithForced:{minPlacedToExploreWithForced}"); }
#endif // DEBUG
        }

        protected override bool CanRunRandomExploration(TriadGameSolver solver, TriadGameSimulationState gameState, int searchLevel)
        {
            int numPlacedThr = (gameState.forcedCardIdx < 0) ? minPlacedToExplore : minPlacedToExploreWithForced;

            return (searchLevel > 0) && (gameState.numCardsPlaced < numPlacedThr);
        }
    }

    /// <summary>
    /// Aguments random search phase with score of game state to increase diffs between probability of initial steps
    /// </summary>
    public class TriadGameAgentCarloScored : TriadGameAgentCarloTheExplorer
    {
        public const float StateWeight = 0.75f;
        public const float StateWeightDecay = 0.25f;

        public const float PriorityDefense = 1.0f;
        public const float PriorityDeck = 2.0f;
        public const float PriorityCapture = 3.5f;

        public override void Initialize(TriadGameSolver solver, int sessionSeed)
        {
            base.Initialize(solver, sessionSeed);
            agentName = "CarloScored";
        }

        protected override SolverResult FindWinningProbability(TriadGameSolver solver, TriadGameSimulationState gameState)
        {
            var result = base.FindWinningProbability(solver, gameState);
            var stateScore = CalculateStateScore(solver, gameState);
            var useWeight = Math.Max(0.0f, StateWeight - ((gameState.deckBlue.numPlaced - 1) * StateWeightDecay));

            var numWinsModified = ((result.numWins / result.numGames) * (1.0f - useWeight)) + (stateScore * useWeight);
            return new SolverResult(Math.Min(1.0f, numWinsModified), result.numDraws / result.numGames, 1);
        }

        public float CalculateStateScore(TriadGameSolver solver, TriadGameSimulationState gameState)
        {
            var (blueDefenseScore, blueCaptureScore) = CalculateBoardScore(solver, gameState);
            var deckScore = CalculateBlueDeckScore(solver, gameState);

#if DEBUG
            if ((debugFlags & DebugFlags.ShowMoveDetailsRng) != DebugFlags.None)
            {
                Logger.WriteLine($"stateScore => def:{blueDefenseScore}, capture:{blueCaptureScore}, deck:{deckScore}");
            }
#endif // DEBUG

            return ((blueDefenseScore * PriorityDefense) + (blueCaptureScore * PriorityCapture) + (deckScore * PriorityDeck)) / (PriorityDefense + PriorityDeck + PriorityCapture);
        }

        private (float, float) CalculateBoardScore(TriadGameSolver solver, TriadGameSimulationState gameState)
        {
            // for each blue card:
            //   for each side:
            //     find all numbers that can capture it
            //   normalize count of capturing numbers
            // normalize card capturing value
            // inverse => blue cards defensive value
            //
            // pct of blue in all cards => capture score

            float capturingSum = 0.0f;
            int numBlueCards = 0;

            for (int idx = 0; idx < gameState.board.Length; idx++)
            {
                var cardInst = gameState.board[idx];
                if (cardInst == null)
                {
                    continue;
                }

                if (cardInst.owner == ETriadCardOwner.Blue)
                {
                    int[] neis = TriadGameSimulation.cachedNeis[idx];

                    int numCapturingValues = 0;
                    int numValidSides = 0;
                    for (int side = 0; side < 4; side++)
                    {
                        if ((neis[side] >= 0) && (gameState.board[neis[side]] == null))
                        {
                            int cardNumber = cardInst.GetNumber((ETriadGameSide)side);
                            int numCaptures = 0;
                            for (int testValue = 1; testValue <= 10; testValue++)
                            {
                                bool canCapture = CanBeCapturedWith(solver.simulation, cardNumber, testValue);
                                numCaptures += canCapture ? 1 : 0;
                            }

                            //Logger.WriteLine($"[{idx}].side:{side} card:{cardNumber} <- captures:{numCaptures}");
                            numValidSides++;
                            numCapturingValues += numCaptures;
                        }
                    }

                    capturingSum += (numValidSides > 0) ? (numCapturingValues / (numValidSides * 10.0f)) : 0.0f;
                    numBlueCards++;
                }
            }

            float defenseScore = (numBlueCards > 0) ? (1.0f - (capturingSum / numBlueCards)) : 0.0f;
            float captureScore = Math.Min(1.0f, numBlueCards / 5.0f);

            return (defenseScore, captureScore);
        }

        private float CalculateBlueDeckScore(TriadGameSolver solver, TriadGameSimulationState gameState)
        {
            float blueCardScore = 0.0f;
            int numScoredBlueCards = 0;

            for (int idx = 0; idx < TriadDeckInstance.maxAvailableCards; idx++)
            {
                if ((gameState.deckBlue.availableCardMask & (1 << idx)) != 0)
                {
                    var testCard = gameState.deckBlue.GetCard(idx);
                    float cardScore = testCard.OptimizerScore;

                    foreach (TriadGameModifier mod in solver.simulation.modifiers)
                    {
                        mod.OnScoreCard(testCard, ref cardScore);
                    }

                    blueCardScore += cardScore;
                    numScoredBlueCards++;
                }
            }

            return (numScoredBlueCards > 0) ? (blueCardScore / numScoredBlueCards) : 0.0f;
        }

        private bool CanBeCapturedWith(TriadGameSimulation simulation, int defendingNum, int capturingNum)
        {
            if ((simulation.modFeatures & TriadGameModifier.EFeature.CaptureWeights) != 0)
            {
                bool isReverseActive = (simulation.modFeatures & TriadGameModifier.EFeature.CaptureMath) != 0;

                foreach (TriadGameModifier mod in simulation.modifiers)
                {
                    mod.OnCheckCaptureCardWeights(null, -1, -1, isReverseActive, ref capturingNum, ref defendingNum);
                }
            }

            bool isCaptured = (capturingNum > defendingNum);

            if ((simulation.modFeatures & TriadGameModifier.EFeature.CaptureMath) != 0)
            {
                foreach (TriadGameModifier mod in simulation.modifiers)
                {
                    mod.OnCheckCaptureCardMath(null, -1, -1, capturingNum, defendingNum, ref isCaptured);
                }
            }

            return isCaptured;
        }
    }
}
