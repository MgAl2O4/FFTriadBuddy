using System;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public class FavDeckSolver
    {
        private TriadDeck deck;
        private TriadGameSession session;
        private TriadNpc npc;
        private int calcId;

        public int contextId;

        public delegate void SolvedDelegate(int id, TriadGameResultChance chance);
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
            if (session != null && session.modifiers.Count == currentGame.modifiers.Count)
            {
                int numMatching = 0;
                for (int Idx = 0; Idx < currentGame.modifiers.Count; Idx++)
                {
                    TriadGameModifier currentMod = session.modifiers[Idx];
                    TriadGameModifier reqMod = currentGame.modifiers[Idx];
                    
                    if (currentMod.GetType() == reqMod.GetType())
                    {
                        numMatching++;
                    }
                }

                isDirty = (numMatching != session.modifiers.Count);
            }

            if (npc != this.npc)
            {
                this.npc = npc;
                isDirty = true;
            }

            if (isDirty)
            {
                session = new TriadGameSession();
                session.solverName = "Fav #" + (contextId + 1);

                foreach (TriadGameModifier mod in currentGame.modifiers)
                {
                    TriadGameModifier modCopy = (TriadGameModifier)Activator.CreateInstance(mod.GetType());
                    modCopy.OnMatchInit();

                    session.modifiers.Add(modCopy);
                }

                session.UpdateSpecialRules();
                CalcWinChance();
            }
        }

        private void CalcWinChance()
        {
            if (session != null && deck != null && npc != null)
            {
                calcId++;

                Task.Run(() =>
                {
                    TriadGameData gameState = session.StartGame(deck, npc.Deck, ETriadGameState.InProgressRed);
                    session.SolverFindBestMove(gameState, out int bestNextPos, out TriadCard bestNextCard, out TriadGameResultChance bestChance);
                    OnSolved(contextId, bestChance);
                });
            }
        }
    }
}
