using System.Collections.Generic;

namespace FFTriadBuddy
{
    public struct SolverResult
    {
        public float numWins;
        public float numDraws;
        public long numGames;

        public float winChance;
        public float drawChance;
        public ETriadGameState expectedResult;
        public float score;

        public static SolverResult Zero = new SolverResult(0, 0, 0);

        public SolverResult(float numWins, float numDraws, long numGames)
        {
            this.numWins = numWins;
            this.numDraws = numDraws;
            this.numGames = numGames;

            winChance = (numGames <= 0) ? 0.0f : (numWins / numGames);
            drawChance = (numGames <= 0) ? 0.0f : (numDraws / numGames);

            if (winChance < 0.25f && drawChance < 0.25f)
            {
                score = winChance / 10.0f;
                expectedResult = ETriadGameState.BlueLost;
            }
            else if (winChance < drawChance)
            {
                score = drawChance;
                expectedResult = ETriadGameState.BlueDraw;
            }
            else
            {
                score = winChance + 10.0f;
                expectedResult = ETriadGameState.BlueWins;
            }
        }

        public bool IsBetterThan(SolverResult other)
        {
            return score > other.score;
        }

        public override string ToString()
        {
            return $"{expectedResult}, score:{score}, win:{winChance:P0} ({numWins:0.##}/{numGames}), draw:{drawChance:P0} ({numDraws:0.##}/{numGames})";
        }
    }

    public class TriadGameSolver
    {
        public TriadGameSimulation simulation = new TriadGameSimulation();
        public TriadGameAgent agent = new TriadGameAgentCarloTheExplorer();
        public string name;

        public TriadGameSolver()
        {
            agent.Initialize(this, 0);
        }

        public void InitializeSimulation(IEnumerable<TriadGameModifier> modsA, IEnumerable<TriadGameModifier> modsB) => simulation.Initialize(modsA, modsB);
        public void InitializeSimulation(IEnumerable<TriadGameModifier> mods) => simulation.Initialize(mods, null);

        public TriadGameSimulationState StartSimulation(TriadDeck deckBlue, TriadDeck deckRed, ETriadGameState state)
        {
            agent.OnSimulationStart();
            return simulation.StartGame(deckBlue, deckRed, state);
        }

        public bool HasSimulationRule(ETriadGameSpecialMod specialRule) => simulation.HasSpecialRule(specialRule);

        public float GetAgentProgress() => agent.GetProgress();

        public bool FindNextMove(TriadGameSimulationState gameState, out int cardIdx, out int boardPos, out SolverResult solverResult) => agent.FindNextMove(this, gameState, out cardIdx, out boardPos, out solverResult);

        public void RunSimulation(TriadGameSimulationState gameState, TriadGameAgent agentBlue, TriadGameAgent agentRed)
        {
            bool keepPlaying = true;
            while (keepPlaying)
            {
                if (gameState.state == ETriadGameState.InProgressBlue)
                {
                    keepPlaying = agentBlue.FindNextMove(this, gameState, out int cardIdx, out int boardPos, out _);
                    if (keepPlaying)
                    {
                        keepPlaying = simulation.PlaceCard(gameState, cardIdx, gameState.deckBlue, ETriadCardOwner.Blue, boardPos);
                    }
                }
                else if (gameState.state == ETriadGameState.InProgressRed)
                {
                    keepPlaying = agentRed.FindNextMove(this, gameState, out int cardIdx, out int boardPos, out _);
                    if (keepPlaying)
                    {
                        keepPlaying = simulation.PlaceCard(gameState, cardIdx, gameState.deckRed, ETriadCardOwner.Red, boardPos);
                    }
                }
                else
                {
                    keepPlaying = false;
                }
            }
        }

        public void FindAvailableActions(TriadGameSimulationState gameState, out int availBoardMask, out int availCardsMask)
        {
            // prepare available board data
            availBoardMask = 0;
            for (int Idx = 0; Idx < gameState.board.Length; Idx++)
            {
                if (gameState.board[Idx] == null)
                {
                    availBoardMask |= (1 << Idx);
                }
            }

            // prepare available cards data
            availCardsMask =
                (gameState.forcedCardIdx >= 0) ? (1 << gameState.forcedCardIdx) :
                (gameState.state == ETriadGameState.InProgressBlue) ? gameState.deckBlue.availableCardMask :
                gameState.deckRed.availableCardMask;

            if ((simulation.modFeatures & TriadGameModifier.EFeature.FilterNext) != 0)
            {
                foreach (var mod in simulation.modifiers)
                {
                    mod.OnFilterNextCards(gameState, ref availCardsMask);
                }
            }
        }

        public void FindAvailableActions(TriadGameSimulationState gameState, out int availBoardMask, out int numAvailBoard, out int availCardsMask, out int numAvailCards)
        {
            FindAvailableActions(gameState, out availBoardMask, out availCardsMask);

            numAvailBoard = CountSetBits(availBoardMask);
            numAvailCards = CountSetBits(availCardsMask);

            int CountSetBits(int value)
            {
                value = value - ((value >> 1) & 0x55555555);
                value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
                return (((value + (value >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
            }
        }
    }
}
