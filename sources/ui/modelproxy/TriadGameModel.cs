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
            public int BoardIdx;
            public TriadGameResultChance WinChance;
        }

        public TriadNpc Npc { get; private set; }
        public TriadDeck PlayerDeck { get; private set; }
        public List<TriadGameModifier> Rules { get; } = new List<TriadGameModifier>();

        public TriadGameSession Session = new TriadGameSession();
        public TriadGameData GameState = null;
        public TriadGameResultChance CachedWinChance;

        public List<TriadGameData> UndoStateRed = new List<TriadGameData>();
        public TriadGameData UndoStateBlue = null;

        public event Action<TriadNpc> OnNpcChanged;
        public event Action<TriadDeck> OnDeckChanged;
        public event Action<TriadGameModel> OnSetupChanged;
        public event Action<TriadGameData, Move> OnGameStateChanged;
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

        public void SetCachedWinChance(TriadGameResultChance winChance)
        {
            CachedWinChance = winChance;
            OnCachedWinChanceChanged?.Invoke(this);
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
            Session = new TriadGameSession();

            // create new instances of modifiers, required for things like roulette
            foreach (var rule in Rules)
            {
                TriadGameModifier modCopy = (TriadGameModifier)Activator.CreateInstance(rule.GetType());
                modCopy.OnMatchInit();

                Session.modifiers.Add(modCopy);
            }

            if (!isTournament)
            {
                foreach (var rule in Npc.Rules)
                {
                    TriadGameModifier modCopy = (TriadGameModifier)Activator.CreateInstance(rule.GetType());
                    modCopy.OnMatchInit();

                    Session.modifiers.Add(modCopy);
                }
            }

            Session.UpdateSpecialRules();
            GameReset();

            if (notifySetupChange)
            {
                OnSetupChanged?.Invoke(this);
            }
        }

        public void GameReset()
        {
            Logger.WriteLine("Game.Reset");
            foreach (var rule in Session.modifiers)
            {
                rule.OnMatchInit();
            }

            GameState = Session.StartGame(PlayerDeck, Npc.Deck, ETriadGameState.InProgressRed);
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
                if ((Session.specialRules & ETriadGameSpecialMod.BlueCardSelection) != ETriadGameSpecialMod.None && blueDeckEx != null)
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
                TriadGameData newUndoState = new TriadGameData(GameState);

                Logger.WriteLine("Red> [{0}]: {1}", boardIdx, card.Name.GetCodeName());
                GameState.bDebugRules = true;
                bool bPlaced = Session.PlaceCard(GameState, card, ETriadCardOwner.Red, boardIdx);
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
                if ((Session.specialRules & ETriadGameSpecialMod.BlueCardSelection) != ETriadGameSpecialMod.None)
                {
                    UndoStateBlue = new TriadGameData(GameState);
                }

                bool bHasMove = Session.SolverFindBestMove(GameState, out int bestNextPos, out TriadCard bestNextCard, out TriadGameResultChance bestChance);
                if (bHasMove)
                {
                    Logger.WriteLine("Blue> [{0}]: {1} => {2}: {3:P0}",
                        bestNextPos, bestNextCard.Name.GetCodeName(),
                        bestChance.expectedResult == ETriadGameState.BlueDraw ? "draw" : "win",
                        bestChance.expectedResult == ETriadGameState.BlueDraw ? bestChance.drawChance : bestChance.winChance);

                    GameState.bDebugRules = true;
                    Session.PlaceCard(GameState, bestNextCard, ETriadCardOwner.Blue, bestNextPos);
                    GameState.bDebugRules = false;

                    OnGameStateChanged?.Invoke(GameState, new Move() { Card = bestNextCard, BoardIdx = bestNextPos, WinChance = bestChance });
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
            Session.UpdateSpecialRules();
            OnSetupChanged?.Invoke(this);
        }
    }
}
