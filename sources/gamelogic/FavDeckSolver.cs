using System;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public class FavDeckSolver
    {
        private TriadDeck deck;
        private TriadGameSimulation currentGame;
        private TriadNpc npc;
        private TriadGameSolver solver;
        public int calcId;

        public int contextId;
        public int progress => (solver == null) ? 0 : (int)(solver.GetAgentProgress() * 100);

        public delegate void SolvedDelegate(int id, TriadDeck deck, SolverResult chance);
        public event SolvedDelegate OnSolved;

        public FavDeckSolver()
        {
            calcId = 0;
        }

        public void SetDeck(TriadDeck deck)
        {
            if (this.deck == null || deck == null || !this.deck.Equals(deck))
            {
                this.deck = deck;
                CalcWinChance();
            }
        }

        public void Update(TriadGameSimulation currentGame, TriadNpc npc)
        {
            bool isDirty = true;
            if (solver != null && solver.simulation.modifiers.Count == currentGame.modifiers.Count)
            {
                int numMatching = 0;
                for (int Idx = 0; Idx < currentGame.modifiers.Count; Idx++)
                {
                    TriadGameModifier currentMod = solver.simulation.modifiers[Idx];
                    TriadGameModifier reqMod = currentGame.modifiers[Idx];

                    if (currentMod.GetType() == reqMod.GetType())
                    {
                        numMatching++;
                    }
                }

                isDirty = (numMatching != solver.simulation.modifiers.Count);
            }

            if (npc != this.npc)
            {
                this.npc = npc;
                isDirty = true;
            }

            if (isDirty)
            {
                this.currentGame = currentGame;
                CalcWinChance();
            }
        }

        private class CalcContext
        {
            public TriadGameSolver solver;
            public TriadGameSimulationState gameState;
            public int calcId;
        }

        private void CalcWinChance()
        {
            if (currentGame != null && deck != null && npc != null)
            {
                calcId++;

                solver = new TriadGameSolver() { name = string.Format("Solv{0}:{1}", contextId + 1, calcId) };
                solver.InitializeSimulation(currentGame.modifiers);

                var gameState = solver.StartSimulation(deck, npc.Deck, ETriadGameState.InProgressRed);
                var calcContext = new CalcContext() { solver = solver, gameState = gameState, calcId = calcId };

                Action<object> solverAction = (ctxOb) =>
                {
                    var ctx = ctxOb as CalcContext;
                    ctx.solver.FindNextMove(ctx.gameState, out var dummyCardIdx, out var dummyBoardPos, out SolverResult bestChance);
                    OnSolved(ctx.calcId, ctx.gameState.deckBlue.deck, bestChance);
                };

                new TaskFactory().StartNew(solverAction, calcContext);
            }
        }
    }
}
