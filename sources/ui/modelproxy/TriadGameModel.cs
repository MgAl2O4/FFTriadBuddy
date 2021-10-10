using MgAl2O4.Utils;
using System;
using System.Collections.Generic;

namespace FFTriadBuddy.UI
{
    // model / state for keeping current game
    public class TriadGameModel
    {
        public class Move
        {
            public TriadCard Card;
            public int CardIdx;
            public int BoardIdx;
            public SolverResult WinChance;
        }

        public TriadNpc Npc { get; private set; }
        public TriadDeck PlayerDeck { get; private set; }
        public List<TriadGameModifier> Rules { get; } = new List<TriadGameModifier>();

        public TriadGameSolver Solver = new TriadGameSolver();
        public TriadGameSimulationState GameState = null;
        public SolverResult CachedWinChance;

        public List<TriadGameSimulationState> UndoStateRed = new List<TriadGameSimulationState>();
        public TriadGameSimulationState UndoStateBlue = null;

        public event Action<TriadNpc> OnNpcChanged;
        public event Action<TriadDeck> OnDeckChanged;
        public event Action<TriadGameModel> OnSetupChanged;
        public event Action<TriadGameSimulationState, Move> OnGameStateChanged;
        public event Action<TriadGameModel> OnCachedWinChanceChanged;

        private bool isTournament = false;

        public TriadGameModel()
        {
            TriadNpc npcOb = TriadNpcDB.Get().npcs.Find(x => x?.Id == PlayerSettingsDB.Get().lastNpcId);
            if (npcOb == null)
            {
                npcOb = TriadNpcDB.Get().Find("Triple Triad master");
            }

            SetNpc(npcOb);
        }

        public void SetNpc(TriadNpc npc)
        {
            if (npc == Npc) { return; }

            Logger.WriteLine("Game.SetNpc: {0}:{1}", npc?.Id, npc?.Name.GetCodeName());
            Npc = npc;
            OnNpcChanged?.Invoke(npc);

            var useDeck = FindDeckToUseFor(npc);
            SetPlayerDeck(useDeck, notifySetupChange: false);

            UpdateSession();

            PlayerSettingsDB.Get().lastNpcId = npc.Id;
        }

        public void SetGameRules(List<TriadGameModifier> mods)
        {
            if (mods.Count == 2)
            {
                SetGameRules(mods[0], mods[1], true);
            }
        }

        public void SetGameRules(TriadGameModifier mod1, TriadGameModifier mod2, bool isTournament = false)
        {
            this.isTournament = isTournament;
            Logger.WriteLine("Game.SetRules: '{0}' + '{1}'{2}",
                mod1?.GetCodeName(), mod2?.GetCodeName(),
                isTournament ? " (tournament mode)" : "");

            Rules.Clear();
            Rules.Add(mod1);
            Rules.Add(mod2);

            UpdateSession();
        }

        public void SetPlayerDeck(TriadDeck deck, bool notifySetupChange = true)
        {
            Logger.WriteLine("Game.SetPlayerDeck: {0}", deck);
            PlayerDeck = deck;

            if (Npc != null && deck != null)
            {
                PlayerSettingsDB.Get().UpdatePlayerDeckForNpc(Npc, deck);
            }

            OnDeckChanged?.Invoke(deck);
            if (notifySetupChange)
            {
                OnSetupChanged?.Invoke(this);
            }
        }

        public void SetCachedWinChance(TriadDeck deck, SolverResult winChance)
        {
            if (PlayerDeck.Equals(deck))
            {
                CachedWinChance = winChance;
                OnCachedWinChanceChanged?.Invoke(this);
            }
        }

        public void ResolveSpecialRule(ETriadGameSpecialMod specialMod)
        {
            GameState.resolvedSpecial |= specialMod;
            OnGameStateChanged?.Invoke(GameState, null);
        }

        private TriadDeck FindDeckToUseFor(TriadNpc npc)
        {
            PlayerSettingsDB settingsDB = PlayerSettingsDB.Get();
            TriadCard[] cardsCopy = null;
            if (settingsDB.lastDeck.ContainsKey(npc))
            {
                TriadDeck savedDeck = PlayerSettingsDB.Get().lastDeck[npc];
                if (savedDeck != null && savedDeck.knownCards.Count == 5)
                {
                    cardsCopy = savedDeck.knownCards.ToArray();
                }
            }

            if (cardsCopy == null)
            {
                cardsCopy = new TriadCard[5];

                if (PlayerDeck == null)
                {
                    Array.Copy(settingsDB.starterCards, cardsCopy, cardsCopy.Length);
                }
                else
                {
                    Array.Copy(PlayerDeck.knownCards.ToArray(), cardsCopy, cardsCopy.Length);
                }
            }

            return new TriadDeck(cardsCopy);
        }

        private void UpdateSession(bool notifySetupChange = true)
        {
            Solver = new TriadGameSolver();
            Solver.InitializeSimulation(Rules, isTournament ? null : Npc.Rules);

            GameReset();

            if (notifySetupChange)
            {
                OnSetupChanged?.Invoke(this);
            }
        }

        public void GameReset()
        {
            Logger.WriteLine("Game.Reset");

            GameState = Solver.StartSimulation(PlayerDeck, Npc.Deck, ETriadGameState.InProgressRed);
            UndoStateBlue = null;
            UndoStateRed.Clear();

            OnGameStateChanged?.Invoke(GameState, null);
        }

        public void GameUndoRed()
        {
            if (UndoStateRed.Count > 0)
            {
                GameState = UndoStateRed[UndoStateRed.Count - 1];
                UndoStateRed.RemoveAt(UndoStateRed.Count - 1);

                OnGameStateChanged?.Invoke(GameState, null);
            }
        }

        public void GameStartBlue()
        {
            if (GameState != null && GameState.numCardsPlaced == 0)
            {
                GameState.state = ETriadGameState.InProgressBlue;
                GamePlayBlueCard();
            }
        }

        public void SetGameForcedBlueCard(TriadCard card)
        {
            if (GameState != null && GameState.state == ETriadGameState.InProgressRed && UndoStateBlue != null)
            {
                var blueDeckEx = GameState.deckBlue as TriadDeckInstanceManual;
                if (Solver.HasSimulationRule(ETriadGameSpecialMod.BlueCardSelection) && blueDeckEx != null)
                {
                    int deckSlotIdx = blueDeckEx.GetCardIndex(card);
                    if (GameState.forcedCardIdx != deckSlotIdx && !blueDeckEx.IsPlaced(deckSlotIdx))
                    {
                        Logger.WriteLine("Force blue card: {0}", card.Name.GetCodeName());

                        GameState = UndoStateBlue;
                        GameState.forcedCardIdx = deckSlotIdx;
                        GamePlayBlueCard();
                    }
                }
            }
        }

        public void SetGameRedCard(TriadCard card, int boardIdx)
        {
            if (GameState != null)
            {
                GameState.forcedCardIdx = -1;
                TriadGameSimulationState newUndoState = new TriadGameSimulationState(GameState);

                Logger.WriteLine("Red> [{0}]: {1}", boardIdx, card.Name.GetCodeName());
                GameState.bDebugRules = true;
                bool bPlaced = Solver.simulation.PlaceCard(GameState, card, ETriadCardOwner.Red, boardIdx);
                GameState.bDebugRules = false;

                // additional debug logs
                int numBoardPlaced = 0;
                {
                    int availBoardMask = 0;
                    for (int Idx = 0; Idx < GameState.board.Length; Idx++)
                    {
                        if (GameState.board[Idx] != null)
                        {
                            numBoardPlaced++;
                        }
                        else
                        {
                            availBoardMask |= (1 << Idx);
                        }
                    }

                    Logger.WriteLine("  Board cards:{0} ({1:x}), placed:{2}", numBoardPlaced, availBoardMask, bPlaced);
                }

                if (bPlaced)
                {
                    if (numBoardPlaced == GameState.board.Length)
                    {
                        OnGameStateChanged?.Invoke(GameState, null);
                    }
                    else
                    {
                        GamePlayBlueCard();
                    }

                    UndoStateRed.Add(newUndoState);
                }
            }
        }

        private void GamePlayBlueCard()
        {
            if (GameState.state == ETriadGameState.InProgressBlue)
            {
                if (Solver.HasSimulationRule(ETriadGameSpecialMod.BlueCardSelection))
                {
                    UndoStateBlue = new TriadGameSimulationState(GameState);
                }

                bool hasMove = Solver.FindNextMove(GameState, out int bestCardIdx, out int bestNextPos, out SolverResult bestChance);
                if (hasMove)
                {
                    var bestCardOb = GameState.deckBlue.GetCard(bestCardIdx);
                    Logger.WriteLine("Blue> [{0}]: {1} => {2}: {3:P0}",
                        bestNextPos, bestCardOb.Name.GetCodeName(),
                        bestChance.expectedResult == ETriadGameState.BlueDraw ? "draw" : "win",
                        bestChance.expectedResult == ETriadGameState.BlueDraw ? bestChance.drawChance : bestChance.winChance);

                    GameState.bDebugRules = true;
                    Solver.simulation.PlaceCard(GameState, bestCardIdx, GameState.deckBlue, ETriadCardOwner.Blue, bestNextPos);
                    GameState.bDebugRules = false;

                    OnGameStateChanged?.Invoke(GameState, new Move() { Card = bestCardOb, CardIdx = bestCardIdx, BoardIdx = bestNextPos, WinChance = bestChance });
                }
                else
                {
                    OnGameStateChanged?.Invoke(GameState, null);
                }
            }
        }

        public void GameRouletteApplied()
        {
            Logger.WriteLine("Game.Roulette applied");
            Solver.simulation.UpdateSpecialRules();
            OnSetupChanged?.Invoke(this);
        }
    }
}
