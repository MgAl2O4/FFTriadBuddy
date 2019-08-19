using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public class TriadGameScreenMemory
    {
        [Flags]
        public enum EUpdateFlags
        {
            None = 0,
            Modifiers = 1,
            Board = 2,
            RedDeck = 4,
            BlueDeck = 8,
        }

        public TriadGameData gameState;
        public TriadGameSession gameSession;
        public TriadDeckInstanceScreen deckBlue;
        public TriadDeckInstanceScreen deckRed;

        public TriadGameScreenMemory()
        {
            gameSession = new TriadGameSession();
            gameState = new TriadGameData();
            deckBlue = new TriadDeckInstanceScreen();
            deckRed = new TriadDeckInstanceScreen();
        }

        public EUpdateFlags OnNewScan(ScreenshotAnalyzer.GameState screenGame, TriadNpc selectedNpc)
        {
            EUpdateFlags updateFlags = EUpdateFlags.None;

            // check if game from screenshot can be continuation of cached one
            // is current state a continuation of last one?
            // ideally each blue turn is a capture until game resets = any card disappears from board
            // guess work for adding sense of persistence to screen decks
            bool bContinuesPrevState = (deckRed.deck == selectedNpc.Deck);
            if (bContinuesPrevState)
            {
                for (int Idx = 0; Idx < gameState.board.Length; Idx++)
                {
                    bool bWasNull = gameState.board[Idx] == null;
                    bool bIsNull = screenGame.board[Idx] == null;

                    if (!bWasNull && bIsNull)
                    {
                        bContinuesPrevState = false;
                        Logger.WriteLine("Can't continue previous state: board[" + Idx + "] disappeared ");
                    }
                }
            }
            else
            {
                Logger.WriteLine("Can't continue previous state: red deck changed");
            }

            bool bModsChanged = (gameSession.modifiers.Count != screenGame.mods.Count) || !gameSession.modifiers.All(screenGame.mods.Contains);
            if (bModsChanged)
            {
                gameSession.modifiers = screenGame.mods;
                gameSession.specialRules = ETriadGameSpecialMod.None;
                gameSession.modFeatures = TriadGameModifier.EFeature.None;
                foreach (TriadGameModifier mod in gameSession.modifiers)
                {
                    gameSession.modFeatures |= mod.GetFeatures();
                }

                updateFlags |= EUpdateFlags.Modifiers;
                bContinuesPrevState = false;
                Logger.WriteLine("Can't continue previous state: modifiers changed");
            }

            bool bRedDeckChanged = !IsDeckMatching(deckRed, screenGame.redDeck) || (deckRed.deck != selectedNpc.Deck);
            if (bRedDeckChanged)
            {
                updateFlags |= EUpdateFlags.RedDeck;
                deckRed.deck = selectedNpc.Deck;

                // needs to happen before any changed to board (gameState)
                UpdateAvailableRedCards(deckRed, screenGame.redDeck, screenGame.blueDeck,
                    screenGame.board, deckBlue.cards, gameState.board, bContinuesPrevState);
            }

            bool bBlueDeckChanged = !IsDeckMatching(deckBlue, screenGame.blueDeck);
            if (bBlueDeckChanged)
            {
                updateFlags |= EUpdateFlags.BlueDeck;
                deckBlue.UpdateAvailableCards(screenGame.blueDeck);
            }

            gameSession.forcedBlueCard = screenGame.forcedBlueCard;
            gameState.state = ETriadGameState.InProgressBlue;
            gameState.deckBlue = deckBlue;
            gameState.deckRed = deckRed;
            gameState.numCardsPlaced = 0;

            bool bBoardChanged = false;
            for (int Idx = 0; Idx < gameState.board.Length; Idx++)
            {
                bool bWasNull = gameState.board[Idx] == null;
                bool bIsNull = screenGame.board[Idx] == null;

                if (bWasNull && !bIsNull)
                {
                    bBoardChanged = true;
                    gameState.board[Idx] = new TriadCardInstance(screenGame.board[Idx], screenGame.boardOwner[Idx]);
                    Logger.WriteLine("  board update: [" + Idx + "] " + gameState.board[Idx].owner + ": " + gameState.board[Idx].card.Name);
                }
                else if (!bWasNull && bIsNull)
                {
                    bBoardChanged = true;
                    gameState.board[Idx] = null;
                }
                else if (!bWasNull && !bIsNull)
                {
                    if (gameState.board[Idx].owner != screenGame.boardOwner[Idx] ||
                        gameState.board[Idx].card != screenGame.board[Idx])
                    {
                        bBoardChanged = true;
                        gameState.board[Idx] = new TriadCardInstance(screenGame.board[Idx], screenGame.boardOwner[Idx]);
                    }
                }

                gameState.numCardsPlaced += (gameState.board[Idx] != null) ? 1 : 0;
            }

            if (bBoardChanged)
            {
                updateFlags |= EUpdateFlags.Board;

                foreach (TriadGameModifier mod in gameSession.modifiers)
                {
                    mod.OnScreenUpdate(gameState);
                }
            }

            Logger.WriteLine("OnNewScan> board:" + (bBoardChanged ? "changed" : "same") +
                ", blue:" + (bBlueDeckChanged ? "changed" : "same") +
                ", red:" + (bRedDeckChanged ? "changed" : "same") +
                ", mods:" + (bModsChanged ? "changed" : "same") +
                ", continuePrev:" + bContinuesPrevState +
                " => " + ((updateFlags != EUpdateFlags.None) ? "UPDATE" : "skip"));

            return updateFlags;
        }

        private bool IsDeckMatching(TriadDeckInstanceScreen deckInstance, TriadCard[] cards)
        {
            bool bIsMatching = false;
            if ((deckInstance != null) && (cards != null) && (deckInstance.cards.Length >= cards.Length))
            {
                bIsMatching = true;
                for (int Idx = 0; Idx < cards.Length; Idx++)
                {
                    bIsMatching = bIsMatching && (cards[Idx] == deckInstance.cards[Idx]);
                }
            }

            return bIsMatching;
        }

        private void UpdateAvailableRedCards(TriadDeckInstanceScreen redDeck,
            TriadCard[] screenCardsRed, TriadCard[] screenCardsBlue, TriadCard[] screenBoard,
            TriadCard[] prevCardsBlue, TriadCardInstance[] prevBoard, bool bContinuePrevState)
        {
            bool bDebugMode = false;
            int hiddenCardId = TriadCardDB.Get().hiddenCard.Id;
            int numVisibleCards = deckRed.cards.Length;

            redDeck.numPlaced = 0;
            if (!bContinuePrevState)
            {
                redDeck.numUnknownPlaced = 0;
            }

            int maxUnknownToUse = redDeck.cards.Length - redDeck.deck.knownCards.Count;
            int firstUnknownPoolIdx = redDeck.cards.Length + redDeck.deck.knownCards.Count;
            if (redDeck.deck.unknownCardPool.Count > 0)
            {
                redDeck.unknownPoolMask = ((1 << redDeck.deck.unknownCardPool.Count) - 1) << firstUnknownPoolIdx;

                for (int Idx = 0; Idx < screenCardsRed.Length; Idx++)
                {
                    if ((screenCardsRed[Idx] != null) && (screenCardsRed[Idx].Id != hiddenCardId) && redDeck.deck.unknownCardPool.Contains(screenCardsRed[Idx]))
                    {
                        redDeck.unknownPoolMask |= (1 << Idx);
                    }
                }
            }

            int allDeckAvailableMask = ((1 << (redDeck.deck.knownCards.Count + redDeck.deck.unknownCardPool.Count)) - 1) << numVisibleCards;

            bool bCanCompareWithPrevData = (screenCardsRed.Length == redDeck.cards.Length) && (screenCardsBlue.Length == prevCardsBlue.Length) && (screenBoard.Length == prevBoard.Length);
            if (bCanCompareWithPrevData && !bContinuePrevState)
            {
                // special case: 1st turn

                int numCardsOnBoard = 0;
                for (int Idx = 0; Idx < screenBoard.Length; Idx++)
                {
                    if (screenBoard[Idx] != null)
                    {
                        numCardsOnBoard++;
                    }
                }

                if (numCardsOnBoard <= 1)
                {
                    bCanCompareWithPrevData = true;
                    prevBoard = new TriadCardInstance[screenBoard.Length];
                    prevCardsBlue = new TriadCard[numVisibleCards];
                    deckRed.cards = new TriadCard[numVisibleCards];
                    deckRed.availableCardMask = allDeckAvailableMask;
                    deckRed.numPlaced = 0;
                    deckRed.numUnknownPlaced = 0;
                }
                else
                {
                    bCanCompareWithPrevData = false;
                }
            }

            if (bDebugMode)
            {
                Logger.WriteLine("Red deck update, diff mode check... " +
                    "bContinuePrevState:" + bContinuePrevState +
                    ", cards(screen:" + screenCardsRed.Length + ", prev:" + deckRed.cards.Length + ")=" + ((screenCardsRed.Length == deckRed.cards.Length) ? "ok" : "nope") +
                    ", other(screen:" + screenCardsBlue.Length + ", prev:" + prevCardsBlue.Length + ")=" + ((screenCardsBlue.Length == prevCardsBlue.Length) ? "ok" : "nope") +
                    ", board(screen:" + screenBoard.Length + ", prev:" + prevBoard.Length + ")=" + ((screenBoard.Length == prevBoard.Length) ? "ok" : "nope"));
            }

            if (bCanCompareWithPrevData)
            {
                // create diffs, hopefully prev state comes from last turn and is just 2 cards away
                List<int> usedCardsIndices = new List<int>();
                List<TriadCard> usedCardsOther = new List<TriadCard>();

                int numKnownOnHand = 0;
                int numUnknownOnHand = 0;
                int numHidden = 0;
                int numOnHand = 0;
                for (int Idx = 0; Idx < deckRed.cards.Length; Idx++)
                {
                    if (screenCardsRed[Idx] == null)
                    {
                        TriadCard prevCard = deckRed.cards[Idx];
                        if ((prevCard != null) && (prevCard.Id != hiddenCardId))
                        {
                            if (bDebugMode) { Logger.WriteLine("  card[" + Idx + "]:" + prevCard.Name + " => mark as used, disappeared from prev state"); }
                            usedCardsIndices.Add(Idx);
                        }

                        deckRed.availableCardMask &= ~(1 << Idx);
                        deckRed.numPlaced++;
                    }
                    else
                    {
                        if (screenCardsRed[Idx].Id != hiddenCardId)
                        {
                            bool bIsUnknown = (deckRed.unknownPoolMask & (1 << Idx)) != 0;
                            numUnknownOnHand += bIsUnknown ? 1 : 0;
                            numKnownOnHand += bIsUnknown ? 0 : 1;
                            numOnHand++;
                            deckRed.availableCardMask |= (1 << Idx);

                            int knownCardIdx = deckRed.deck.knownCards.IndexOf(screenCardsRed[Idx]);
                            int unknownCardIdx = deckRed.deck.unknownCardPool.IndexOf(screenCardsRed[Idx]);
                            if (knownCardIdx >= 0)
                            {
                                deckRed.availableCardMask &= ~(1 << (knownCardIdx + deckRed.cards.Length));
                            }
                            else if (unknownCardIdx >= 0)
                            {
                                deckRed.availableCardMask &= ~(1 << (unknownCardIdx + deckRed.cards.Length + deckRed.deck.knownCards.Count));
                            }

                            if (bDebugMode)
                            {
                                TriadCard cardOb = screenCardsRed[Idx];
                                Logger.WriteLine(" card[" + Idx + "]:" + (cardOb != null ? cardOb.Name : "??") +
                                    " => numUnknown:" + numUnknownOnHand + ", numKnown:" + numKnownOnHand + ", numHidden:" + numHidden);
                            }
                        }
                        else
                        {
                            numHidden++;
                        }
                    }
                }

                for (int Idx = 0; Idx < prevCardsBlue.Length; Idx++)
                {
                    if ((prevCardsBlue[Idx] != null) && (screenCardsBlue[Idx] == null))
                    {
                        usedCardsOther.Add(prevCardsBlue[Idx]);
                        if (bDebugMode) { Logger.WriteLine("  blue[" + Idx + "]:" + prevCardsBlue[Idx].Name + " => mark as used"); }
                    }
                }

                for (int Idx = 0; Idx < prevBoard.Length; Idx++)
                {
                    TriadCard testCard = screenBoard[Idx];
                    if ((prevBoard[Idx] == null || prevBoard[Idx].card == null) && (testCard != null))
                    {
                        int testCardIdx = deckRed.GetCardIndex(testCard);
                        if (!usedCardsOther.Contains(testCard) && (testCardIdx >= 0))
                        {
                            usedCardsIndices.Add(testCardIdx);
                            if (bDebugMode) { Logger.WriteLine("  card[" + testCardIdx + "]:" + testCard.Name + " => mark as used, appeared on board[" + Idx + "], not used by blue"); }
                        }
                    }
                }

                deckRed.cards = screenCardsRed;

                for (int Idx = 0; Idx < usedCardsIndices.Count; Idx++)
                {
                    int cardMask = 1 << usedCardsIndices[Idx];
                    deckRed.availableCardMask &= ~cardMask;

                    bool bIsUnknownPool = (deckRed.unknownPoolMask & cardMask) != 0;
                    if (bIsUnknownPool)
                    {
                        deckRed.numUnknownPlaced++;
                    }

                    if (bDebugMode)
                    {
                        TriadCard cardOb = deckRed.GetCard(usedCardsIndices[Idx]);
                        Logger.WriteLine(" card[" + usedCardsIndices[Idx] + "]:" + (cardOb != null ? cardOb.Name : "??") + " => used");
                    }
                }

                if ((numHidden == 0) && ((numOnHand + deckRed.numPlaced) == numVisibleCards))
                {
                    deckRed.availableCardMask &= (1 << numVisibleCards) - 1;
                    if (bDebugMode) { Logger.WriteLine("   all cards are on hand and visible"); }
                }
                else if ((deckRed.numUnknownPlaced + numUnknownOnHand) >= maxUnknownToUse ||
                    ((numKnownOnHand >= (numVisibleCards - maxUnknownToUse)) && (numHidden == 0)))
                {
                    deckRed.availableCardMask &= (1 << (numVisibleCards + deckRed.deck.knownCards.Count)) - 1;

                    if (bDebugMode)
                    {
                        Logger.WriteLine("   removing all unknown cards, numUnknownPlaced:" + deckRed.numUnknownPlaced +
                            ", numUnknownOnHand:" + numUnknownOnHand + ", numKnownOnHand:" + numKnownOnHand +
                            ", numHidden:" + numHidden + ", maxUnknownToUse:" + maxUnknownToUse);
                    }
                }
            }
            else
            {
                // TriadDeckInstanceScreen is mostly stateless (created from scratch on screen capture)
                // this makes guessing which cards were placed hard, especially when there's no good
                // history data to compare with. 
                // Ignore board data here, cards could be placed by blue and are still available for red deck

                deckRed.UpdateAvailableCards(screenCardsRed);
                deckRed.availableCardMask = allDeckAvailableMask;
            }

            if (bDebugMode)
            {
                redDeck.LogAvailableCards("Red deck");
            }
        }        
    }
}
