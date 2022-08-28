using MgAl2O4.Utils;
using System;
using System.Collections.Generic;

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

    public class TriadGameSimulationState
    {
        public TriadCardInstance[] board;
        public TriadDeckInstance deckBlue;
        public TriadDeckInstance deckRed;
        public ETriadGameState state;
        public ETriadGameSpecialMod resolvedSpecial;
        public int[] typeMods;
        public int numCardsPlaced;
        public int numRestarts;
        public int forcedCardIdx;
        public bool bDebugRules;

        public const int boardSize = 3;
        public const int boardSizeSq = boardSize * boardSize;

        public TriadGameSimulationState()
        {
            board = new TriadCardInstance[boardSizeSq];
            typeMods = new int[Enum.GetNames(typeof(ETriadCardType)).Length];
            state = ETriadGameState.InProgressBlue;
            resolvedSpecial = ETriadGameSpecialMod.None;
            numCardsPlaced = 0;
            numRestarts = 0;
            forcedCardIdx = -1;
            bDebugRules = false;

            for (int Idx = 0; Idx < typeMods.Length; Idx++)
            {
                typeMods[Idx] = 0;
            }
        }

        public TriadGameSimulationState(TriadGameSimulationState copyFrom)
        {
            board = new TriadCardInstance[copyFrom.board.Length];
            for (int Idx = 0; Idx < board.Length; Idx++)
            {
                board[Idx] = (copyFrom.board[Idx] == null) ? null : new TriadCardInstance(copyFrom.board[Idx]);
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
            // bDebugRules not copied, only first step needs it
        }
    }

    public class TriadGameSimulation
    {
        public List<TriadGameModifier> modifiers = new List<TriadGameModifier>();
        public ETriadGameSpecialMod specialRules;
        public TriadGameModifier.EFeature modFeatures = TriadGameModifier.EFeature.None;

        public static int[][] cachedNeis = new int[9][];

        public TriadGameSimulationState StartGame(TriadDeck deckBlue, TriadDeck deckRed, ETriadGameState state)
        {
            foreach (var mod in modifiers)
            {
                mod.OnMatchInit();
            }

            return new TriadGameSimulationState()
            {
                state = state,
                deckBlue = new TriadDeckInstanceManual(deckBlue),
                deckRed = new TriadDeckInstanceManual(deckRed)
            };
        }

        public void Initialize(IEnumerable<TriadGameModifier> modsA, IEnumerable<TriadGameModifier> modsB = null)
        {
            modifiers.Clear();

            if (modsA != null)
            {
                foreach (var mod in modsA)
                {
                    TriadGameModifier modCopy = (TriadGameModifier)Activator.CreateInstance(mod.GetType());
                    modifiers.Add(modCopy);
                }
            }

            if (modsB != null)
            {
                foreach (var mod in modsB)
                {
                    TriadGameModifier modCopy = (TriadGameModifier)Activator.CreateInstance(mod.GetType());
                    modifiers.Add(modCopy);
                }
            }

            UpdateSpecialRules();
        }

        public void UpdateSpecialRules()
        {
            specialRules = ETriadGameSpecialMod.None;
            modFeatures = TriadGameModifier.EFeature.None;
            foreach (TriadGameModifier mod in modifiers)
            {
                specialRules |= mod.GetSpecialRules();
                modFeatures |= mod.GetFeatures();
            }
        }

        public bool HasSpecialRule(ETriadGameSpecialMod specialRule)
        {
            return (specialRules & specialRule) != ETriadGameSpecialMod.None;
        }

        public bool PlaceCard(TriadGameSimulationState gameState, int cardIdx, TriadDeckInstance cardDeck, ETriadCardOwner owner, int boardPos)
        {
            bool bResult = false;

            bool bIsAllowedOwner =
                ((owner == ETriadCardOwner.Blue) && (gameState.state == ETriadGameState.InProgressBlue)) ||
                ((owner == ETriadCardOwner.Red) && (gameState.state == ETriadGameState.InProgressRed));

            TriadCard card = cardDeck.GetCard(cardIdx);
            if (bIsAllowedOwner && (boardPos >= 0) && (gameState.board[boardPos] == null) && (card != null))
            {
                gameState.board[boardPos] = new TriadCardInstance(card, owner);
                gameState.numCardsPlaced++;

                if (owner == ETriadCardOwner.Blue)
                {
                    gameState.deckBlue.OnCardPlacedFast(cardIdx);
                    gameState.state = ETriadGameState.InProgressRed;
                }
                else
                {
                    gameState.deckRed.OnCardPlacedFast(cardIdx);
                    gameState.state = ETriadGameState.InProgressBlue;
                }

                // verify owner
                bResult = (owner == ETriadCardOwner.Red) || !HasSpecialRule(ETriadGameSpecialMod.IgnoreOwnedCheck);

                bool bAllowCombo = false;
                if ((modFeatures & TriadGameModifier.EFeature.CardPlaced) != 0)
                {
                    foreach (TriadGameModifier mod in modifiers)
                    {
                        mod.OnCardPlaced(gameState, boardPos);
                        bAllowCombo = bAllowCombo || mod.AllowsCombo();
                    }
                }

                List<int> comboList = new List<int>();
                int comboCounter = 0;
                CheckCaptures(gameState, boardPos, comboList, comboCounter);

                while (bAllowCombo && comboList.Count > 0)
                {
                    if (gameState.bDebugRules) { Logger.WriteLine(">> combo step: {0}", string.Join(",", comboList)); }

                    List<int> nextCombo = new List<int>();
                    comboCounter++;
                    foreach (int pos in comboList)
                    {
                        CheckCaptures(gameState, pos, nextCombo, comboCounter);
                    }

                    comboList = nextCombo;
                }

                if ((modFeatures & TriadGameModifier.EFeature.PostCapture) != 0)
                {
                    foreach (TriadGameModifier mod in modifiers)
                    {
                        mod.OnPostCaptures(gameState, boardPos);
                    }
                }

                if (gameState.numCardsPlaced == gameState.board.Length)
                {
                    OnAllCardsPlaced(gameState);
                }
            }

            return bResult;
        }

        public bool PlaceCard(TriadGameSimulationState gameState, TriadCard card, ETriadCardOwner owner, int boardPos)
        {
            TriadDeckInstance useDeck = (owner == ETriadCardOwner.Blue) ? gameState.deckBlue : gameState.deckRed;
            int cardIdx = useDeck.GetCardIndex(card);

            return PlaceCard(gameState, cardIdx, useDeck, owner, boardPos);
        }

        public static int GetBoardPos(int x, int y)
        {
            return x + (y * TriadGameSimulationState.boardSize);
        }

        public static void GetBoardXY(int pos, out int x, out int y)
        {
            x = pos % TriadGameSimulationState.boardSize;
            y = pos / TriadGameSimulationState.boardSize;
        }

        public static int[] GetNeighbors(TriadGameSimulationState gameState, int boardPos)
        {
            int boardPosX = 0;
            int boardPosY = 0;
            GetBoardXY(boardPos, out boardPosX, out boardPosY);

            int[] resultNeis = new int[4];
            resultNeis[(int)ETriadGameSide.Up] = (boardPosY > 0) ? GetBoardPos(boardPosX, boardPosY - 1) : -1;
            resultNeis[(int)ETriadGameSide.Down] = (boardPosY < (TriadGameSimulationState.boardSize - 1)) ? GetBoardPos(boardPosX, boardPosY + 1) : -1;
            resultNeis[(int)ETriadGameSide.Right] = (boardPosX > 0) ? GetBoardPos(boardPosX - 1, boardPosY) : -1;
            resultNeis[(int)ETriadGameSide.Left] = (boardPosX < (TriadGameSimulationState.boardSize - 1)) ? GetBoardPos(boardPosX + 1, boardPosY) : -1;

            return resultNeis;
        }

        private void CheckCaptures(TriadGameSimulationState gameState, int boardPos, List<int> comboList, int comboCounter)
        {
            // combo:
            // - modifiers are active only in intial placement
            // - only card captured via modifiers can initiate combo (same, plus)
            // - type modifiers (ascention, descention) values are baked in card and influence combo
            // - can't proc another plus/same as a result of combo

            int[] neis = cachedNeis[boardPos];
            bool allowMods = comboCounter == 0;
            if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureNei) != 0)
            {
                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnCheckCaptureNeis(gameState, boardPos, neis, comboList);
                }
            }

            // CaptureMath: used only by reverse rule
            bool isReverseActive = allowMods && ((modFeatures & TriadGameModifier.EFeature.CaptureMath) != 0);

            TriadCardInstance checkCard = gameState.board[boardPos];
            for (int sideIdx = 0; sideIdx < 4; sideIdx++)
            {
                int neiPos = neis[sideIdx];
                if (neiPos >= 0 && gameState.board[neiPos] != null)
                {
                    TriadCardInstance neiCard = gameState.board[neiPos];
                    if (checkCard.owner != neiCard.owner)
                    {
                        int numPos = checkCard.GetNumber((ETriadGameSide)sideIdx);
                        int numOther = neiCard.GetOppositeNumber((ETriadGameSide)sideIdx);

                        if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureWeights) != 0)
                        {
                            // CaptureWeights: use only by fallen ace = asymetric rule, needs to know about active reverse
                            foreach (TriadGameModifier mod in modifiers)
                            {
                                mod.OnCheckCaptureCardWeights(gameState, boardPos, neiPos, isReverseActive, ref numPos, ref numOther);
                            }
                        }

                        bool bIsCaptured = (numPos > numOther);
                        if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureMath) != 0)
                        {
                            foreach (TriadGameModifier mod in modifiers)
                            {
                                mod.OnCheckCaptureCardMath(gameState, boardPos, neiPos, numPos, numOther, ref bIsCaptured);
                            }
                        }

                        if (bIsCaptured)
                        {
                            neiCard.owner = checkCard.owner;
                            if (comboCounter > 0)
                            {
                                comboList.Add(neiPos);
                            }

                            if (gameState.bDebugRules)
                            {
                                Logger.WriteLine(">> " + (comboCounter > 0 ? "combo!" : "") + " [" + neiPos + "] " + neiCard.card.Name.GetCodeName() + " => " + neiCard.owner);
                            }
                        }
                    }
                }
            }
        }

        private void OnAllCardsPlaced(TriadGameSimulationState gameState)
        {
            int numBlue = (gameState.deckBlue.availableCardMask != 0) ? 1 : 0;
            foreach (TriadCardInstance card in gameState.board)
            {
                if (card.owner == ETriadCardOwner.Blue)
                {
                    numBlue++;
                }
            }

            int numBlueToWin = (gameState.board.Length / 2) + 1;
            gameState.state = (numBlue > numBlueToWin) ? ETriadGameState.BlueWins :
                (numBlue == numBlueToWin) ? ETriadGameState.BlueDraw :
                ETriadGameState.BlueLost;

            if (gameState.bDebugRules)
            {
                TriadCard availBlueCard = gameState.deckBlue.GetFirstAvailableCard();
                Logger.WriteLine(">> blue:" + numBlue + " (in deck:" + ((availBlueCard != null) ? availBlueCard.Name.GetCodeName() : "none") + "), required:" + numBlueToWin + " => " + gameState.state);
            }

            if ((modFeatures & TriadGameModifier.EFeature.AllPlaced) != 0)
            {
                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnAllCardsPlaced(gameState);
                }
            }
        }

        public static void StaticInitialize()
        {
            for (int idxPos = 0; idxPos < 9; idxPos++)
            {
                cachedNeis[idxPos] = GetNeighbors(null, idxPos);
            }
        }
    }
}
