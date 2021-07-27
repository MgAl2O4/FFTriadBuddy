using System;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public class FavDeckSolver
    {
        private TriadDeck deck;
        private TriadGameSession currentGame;
        private TriadNpc npc;
        private TriadGameSession solverSession;
        public int calcId;

        public int contextId;
        public int progress => solverSession?.currentProgress ?? 0;

        public delegate void SolvedDelegate(int id, TriadDeck deck, TriadGameResultChance chance);
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

        public void Update(TriadGameSession currentGame, TriadNpc npc)
        {
            bool isDirty = true;
            if (solverSession != null && solverSession.modifiers.Count == currentGame.modifiers.Count)
            {
                int numMatching = 0;
                for (int Idx = 0; Idx < currentGame.modifiers.Count; Idx++)
                {
                    TriadGameModifier currentMod = solverSession.modifiers[Idx];
                    TriadGameModifier reqMod = currentGame.modifiers[Idx];

                    if (currentMod.GetType() == reqMod.GetType())
                    {
                        numMatching++;
                    }
                }

                isDirty = (numMatching != solverSession.modifiers.Count);
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
            public TriadGameSession session;
            public TriadGameData gameData;
            public int calcId;
        }

        private void CalcWinChance()
        {
            if (currentGame != null && deck != null && npc != null)
            {
                calcId++;

                solverSession = new TriadGameSession() { solverName = string.Format("Solv{0}:{1}", contextId + 1, calcId) };
                foreach (TriadGameModifier mod in currentGame.modifiers)
                {
                    TriadGameModifier modCopy = (TriadGameModifier)Activator.CreateInstance(mod.GetType());
                    modCopy.OnMatchInit();

                    solverSession.modifiers.Add(modCopy);
                }

                solverSession.UpdateSpecialRules();

                var gameData = solverSession.StartGame(deck, npc.Deck, ETriadGameState.InProgressRed);
                var calcContext = new CalcContext() { session = solverSession, gameData = gameData, calcId = calcId };

                Action<object> solverAction = (ctxOb) =>
                {
                    var ctx = ctxOb as CalcContext;
                    ctx.session.SolverFindBestMove(ctx.gameData, out int bestNextPos, out TriadCard bestNextCard, out TriadGameResultChance bestChance);
                    OnSolved(ctx.calcId, ctx.gameData.deckBlue.deck, bestChance);
                };

                new TaskFactory().StartNew(solverAction, calcContext);
            }
        }
    }
}
