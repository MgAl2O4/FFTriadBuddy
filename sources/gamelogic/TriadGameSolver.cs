using System.Collections.Generic;

namespace FFTriadBuddy
{
    public struct SolverResult
    {
        public float winChance;
        public float drawChance;
        public ETriadGameState expectedResult;
        public float compScore;

        public static SolverResult Zero = new SolverResult(0.0f, 0.0f);

        public SolverResult(float winChance, float drawChance)
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

        public bool IsBetterThan(SolverResult other)
        {
            return compScore > other.compScore;
        }
    }

    public class TriadGameSolver
    {
        public TriadGameSimulation simulation = new TriadGameSimulation();
        public TriadGameAgent agent = new TriadGameAgentDerpyCarlo();
        public string name;

        public void InitializeSimulation(IEnumerable<TriadGameModifier> modsA, IEnumerable<TriadGameModifier> modsB) => simulation.Initialize(modsA, modsB);
        public void InitializeSimulation(IEnumerable<TriadGameModifier> mods) => simulation.Initialize(mods, null);

        public TriadGameSimulationState StartSimulation(TriadDeck deckBlue, TriadDeck deckRed, ETriadGameState state) => simulation.StartGame(deckBlue, deckRed, state);

        public bool HasSimulationRule(ETriadGameSpecialMod specialRule) => simulation.HasSpecialRule(specialRule);

        public float GetAgentProgress() => agent.GetProgress();

        public bool FindNextMove(TriadGameSimulationState gameState, out int cardIdx, out int boardPos, out SolverResult solverResult, bool debugMode = false)
            => agent.FindNextMove(this, gameState, out cardIdx, out boardPos, out solverResult, debugMode);

        public void RunSimulation(TriadGameSimulationState gameState, TriadGameAgent agentBlue, TriadGameAgent agentRed)
        {
            bool keepPlaying = true;
            while (keepPlaying)
            {
                if (gameState.state == ETriadGameState.InProgressBlue)
                {
                    keepPlaying = agentBlue.FindNextMove(this, gameState, out int cardIdx, out int boardPos, out var dummyResult);
                    if (keepPlaying)
                    {
                        keepPlaying = simulation.PlaceCard(gameState, cardIdx, gameState.deckBlue, ETriadCardOwner.Blue, boardPos);
                    }
                }
                else if (gameState.state == ETriadGameState.InProgressRed)
                {
                    keepPlaying = agentRed.FindNextMove(this, gameState, out int cardIdx, out int boardPos, out var dummyResult);
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

        public void FindAvailableActions(TriadGameSimulationState gameState, out int availBoardMask, out int numAvailBoard, out int availCardsMask, out int numAvailCards)
        {
            // prepare available board data
            availBoardMask = 0;
            numAvailBoard = 0;
            for (int Idx = 0; Idx < gameState.board.Length; Idx++)
            {
                if (gameState.board[Idx] == null)
                {
                    availBoardMask |= (1 << Idx);
                    numAvailBoard++;
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

            numAvailCards = 0;
            for (int Idx = 0; Idx < TriadDeckInstance.maxAvailableCards; Idx++)
            {
                numAvailCards += ((availCardsMask & (1 << Idx)) != 0) ? 1 : 0;
            }
        }
    }
}
