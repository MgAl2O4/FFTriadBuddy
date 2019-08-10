using System.Collections.Generic;

namespace FFTriadBuddy
{
    public enum ETriadDeckState
    {
        Valid,
        MissingCards,
        HasDuplicates,
        TooManyRaresUncomon,
        TooManyRaresRare,
        TooManyRaresEpic,
    };

    public class TriadDeck
    {
        public List<TriadCard> knownCards;
        public List<TriadCard> unknownCardPool;

        public TriadDeck()
        {
            knownCards = new List<TriadCard>();
            unknownCardPool = new List<TriadCard>();
        }

        public TriadDeck(List<TriadCard> knownCards, List<TriadCard> unknownCardPool)
        {
            this.knownCards = new List<TriadCard>();
            this.unknownCardPool = new List<TriadCard>();

            this.knownCards.AddRange(knownCards);
            this.unknownCardPool.AddRange(unknownCardPool);
        }

        public TriadDeck(IEnumerable<TriadCard> knownCards)
        {
            this.knownCards = new List<TriadCard>();
            unknownCardPool = new List<TriadCard>();

            this.knownCards.AddRange(knownCards);
        }

        public TriadDeck(IEnumerable<int> knownCardIds, IEnumerable<int> unknownCardlIds)
        {
            TriadCardDB cardDB = TriadCardDB.Get();

            knownCards = new List<TriadCard>();
            foreach (int id in knownCardIds)
            {
                TriadCard card = cardDB.cards[id];
                if (card != null && card.IsValid())
                {
                    knownCards.Add(card);
                }
            }

            unknownCardPool = new List<TriadCard>();
            foreach (int id in unknownCardlIds)
            {
                TriadCard card = cardDB.cards[id];
                if (card != null && card.IsValid())
                {
                    unknownCardPool.Add(card);
                }
            }
        }

        public TriadDeck(IEnumerable<int> knownCardIds)
        {
            TriadCardDB cardDB = TriadCardDB.Get();

            knownCards = new List<TriadCard>();
            foreach (int id in knownCardIds)
            {
                TriadCard card = cardDB.cards[id];
                if (card != null && card.IsValid())
                {
                    knownCards.Add(card);
                }
            }

            unknownCardPool = new List<TriadCard>();
        }

        public ETriadDeckState GetDeckState()
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            int[] rarityCounters = new int[5];

            for (int DeckIdx = 0; DeckIdx < knownCards.Count; DeckIdx++)
            {
                TriadCard deckCard = knownCards[DeckIdx];
                bool bIsOwned = playerDB.ownedCards.Contains(deckCard);
                if (!bIsOwned)
                {
                    return ETriadDeckState.MissingCards;
                }

                for (int TestIdx = 0; TestIdx < knownCards.Count; TestIdx++)
                {
                    if ((TestIdx != DeckIdx) && knownCards[TestIdx].Equals(deckCard))
                    {
                        return ETriadDeckState.HasDuplicates;
                    }
                }

                rarityCounters[(int)deckCard.Rarity]++;
            }

            int numRare45 = rarityCounters[(int)ETriadCardRarity.Epic] + rarityCounters[(int)ETriadCardRarity.Legendary];
            int numRare345 = rarityCounters[(int)ETriadCardRarity.Rare] + numRare45;
            int numRare2345 = rarityCounters[(int)ETriadCardRarity.Uncommon] + numRare345;

            if (playerDB.ownedCards.Count < 30)
            {
                if (numRare2345 > 1)
                {
                    return ETriadDeckState.TooManyRaresUncomon;
                }
            }
            else if (playerDB.ownedCards.Count < 60)
            {
                if (numRare345 > 1)
                {
                    return ETriadDeckState.TooManyRaresRare;
                }
            }
            else
            {
                if (numRare45 > 1)
                {
                    return ETriadDeckState.TooManyRaresEpic;
                }
            }

            return ETriadDeckState.Valid;
        }

        public TriadCard GetCard(int Idx)
        {
            if (Idx < knownCards.Count)
            {
                return knownCards[Idx];
            }
            else if (Idx < (knownCards.Count + unknownCardPool.Count))
            {
                return unknownCardPool[Idx - knownCards.Count];
            }

            return null;
        }

        public bool SetCard(int Idx, TriadCard card)
        {
            bool bResult = false;
            if (Idx < knownCards.Count)
            {
                knownCards[Idx] = card;
                bResult = true;
            }
            else if (Idx < (knownCards.Count + unknownCardPool.Count))
            {
                unknownCardPool[Idx - knownCards.Count] = card;
                bResult = true;
            }

            return bResult;
        }

        public int GetPower()
        {
            int SumRating = 0;
            foreach (TriadCard card in knownCards)
            {
                SumRating += (int)card.Rarity + 1;
            }

            foreach (TriadCard card in unknownCardPool)
            {
                SumRating += (int)card.Rarity + 1;
            }

            int NumCards = knownCards.Count + unknownCardPool.Count;
            int DeckPower = (NumCards > 0) ? System.Math.Min(System.Math.Max((SumRating * 2 / NumCards), 1), 10) : 1;

            return DeckPower;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TriadDeck);
        }

        public bool Equals(TriadDeck otherDeck)
        {
            if ((knownCards.Count != otherDeck.knownCards.Count) ||
                (unknownCardPool.Count != otherDeck.unknownCardPool.Count))
            {
                return false;
            }

            for (int Idx = 0; Idx < knownCards.Count; Idx++)
            {
                if (!knownCards[Idx].Equals(otherDeck.knownCards[Idx]))
                {
                    return false;
                }
            }

            for (int Idx = 0; Idx < unknownCardPool.Count; Idx++)
            {
                if (!unknownCardPool[Idx].Equals(otherDeck.unknownCardPool[Idx]))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = 739328532;
            hashCode = hashCode * -1521134295 + EqualityComparer<List<TriadCard>>.Default.GetHashCode(knownCards);
            hashCode = hashCode * -1521134295 + EqualityComparer<List<TriadCard>>.Default.GetHashCode(unknownCardPool);
            return hashCode;
        }

        public override string ToString()
        {
            string desc = "";
            foreach (TriadCard card in knownCards)
            {
                desc += card.ToShortString() + ", ";
            }

            desc = (desc.Length > 2) ? desc.Remove(desc.Length - 2, 2) : "(none)";

            if (unknownCardPool.Count > 0)
            {
                desc += " + unknown(";
                foreach (TriadCard card in unknownCardPool)
                {
                    desc += card.ToShortString() + ", ";
                }

                desc = desc.Remove(desc.Length - 2, 2);
                desc += ")";
            }

            int power = GetPower();
            desc += ", power:" + power;

            return desc;
        }
    }

    public abstract class TriadDeckInstance
    {
        public abstract void OnCardPlaced(TriadCard card);
        public abstract TriadCard[] GetAvailableCards();
        public abstract TriadCard GetFirstAvailableCard();
        public abstract TriadDeckInstance CreateCopy();
    }

    public class TriadDeckInstanceManual : TriadDeckInstance
    {
        public readonly TriadDeck deck;
        public List<TriadCard> placedCards;
        public int numUnknownPlaced;

        public TriadDeckInstanceManual(TriadDeck deck)
        {
            this.deck = deck;
            placedCards = new List<TriadCard>();
            numUnknownPlaced = 0;
        }

        public TriadDeckInstanceManual(TriadDeckInstanceManual copyFrom)
        {
            deck = copyFrom.deck;
            placedCards = new List<TriadCard>();
            placedCards.AddRange(copyFrom.placedCards);
            numUnknownPlaced = copyFrom.numUnknownPlaced;
        }

        public override TriadDeckInstance CreateCopy()
        {
            TriadDeckInstanceManual deckCopy = new TriadDeckInstanceManual(this);
            return deckCopy;
        }

        public override void OnCardPlaced(TriadCard card)
        {
            placedCards.Add(card);

            if (deck.unknownCardPool.Contains(card))
            {
                numUnknownPlaced++;
            }
        }

        public override TriadCard[] GetAvailableCards()
        {
            List<TriadCard> result = new List<TriadCard>();
            foreach (TriadCard card in deck.knownCards)
            {
                result.Add(card);
            }

            int maxCardsToPlace = ((TriadGameData.boardSize * TriadGameData.boardSize) / 2) + 1;
            int maxUnknownToPlace = maxCardsToPlace - deck.knownCards.Count;
            int numUnknownToPlace = maxUnknownToPlace - numUnknownPlaced;
            if (numUnknownToPlace > 0)
            {
                foreach (TriadCard card in deck.unknownCardPool)
                {
                    result.Add(card);
                }
            }

            foreach (TriadCard card in placedCards)
            {
                result.Remove(card);
            }

            return (result.Count > 0) ? result.ToArray() : null;
        }

        public override TriadCard GetFirstAvailableCard()
        {
            foreach (TriadCard card in deck.knownCards)
            {
                if (!placedCards.Contains(card))
                {
                    return card;
                }
            }

            return null;
        }

        public override string ToString()
        {
            string desc = "Placed: " + placedCards.Count + ", Available: ";

            TriadCard[] availCards = GetAvailableCards();
            if (availCards != null)
            {
                foreach (TriadCard card in availCards)
                {
                    desc += card.ToShortString() + ", ";
                }

                desc = desc.Remove(desc.Length - 2, 2);
            }
            else
            {
                desc += "none";
            }

            return desc;
        }
    }

    public class TriadDeckInstanceScreen : TriadDeckInstance
    {
        public TriadCard[] cards;
        public TriadDeck npcDeck;

        public TriadDeckInstanceScreen()
        {
            cards = new TriadCard[5];
        }

        public TriadDeckInstanceScreen(TriadDeckInstanceScreen copyFrom)
        {
            cards = new TriadCard[copyFrom.cards.Length];
            for (int Idx = 0; Idx < copyFrom.cards.Length; Idx++)
            {
                cards[Idx] = copyFrom.cards[Idx];
            }

            npcDeck = copyFrom.npcDeck;
        }

        public override TriadDeckInstance CreateCopy()
        {
            TriadDeckInstanceScreen deckCopy = new TriadDeckInstanceScreen(this);
            return deckCopy;
        }

        public override TriadCard[] GetAvailableCards()
        {
            int hiddenCardId = TriadCardDB.Get().hiddenCard.Id;
            int numHidden = 0;

            List<TriadCard> cardList = new List<TriadCard>();
            foreach (TriadCard card in cards)
            {
                if (card != null)
                {
                    if (card.Id == hiddenCardId)
                    {
                        numHidden++;
                    }
                    else
                    {
                        cardList.Add(card);
                    }
                }
            }

            if ((numHidden > 0) && (npcDeck != null))
            {
                cardList.AddRange(npcDeck.unknownCardPool);
            }

            return cardList.ToArray();
        }

        public override TriadCard GetFirstAvailableCard()
        {
            foreach (TriadCard card in cards)
            {
                if (card != null)
                {
                    return card;
                }
            }

            return null;
        }

        public override void OnCardPlaced(TriadCard card)
        {
            for (int Idx = 0; Idx < cards.Length; Idx++)
            {
                if (cards[Idx] == card)
                {
                    cards[Idx] = null;
                    break;
                }
            }
        }

        public override string ToString()
        {
            string desc = "[SCREEN] Available: ";

            int numAvail = 0;
            int numHidden = 0;
            if (cards != null)
            {
                int hiddenCardId = TriadCardDB.Get().hiddenCard.Id;
                foreach (TriadCard card in cards)
                {
                    if (card != null)
                    {
                        desc += card.ToShortString() + ", ";
                        numAvail++;
                        numHidden += (card.Id == hiddenCardId) ? 1 : 0;
                    }
                }
            }

            if (numAvail == 0)
            {
                desc += "none";
            }
            else
            {
                desc = desc.Remove(desc.Length - 2, 2);
            }

            if (numHidden > 0)
            {
                desc += ", Unknown: ";
                if (npcDeck != null && npcDeck.unknownCardPool.Count > 0)
                {
                    foreach (TriadCard card in npcDeck.unknownCardPool)
                    {
                        desc += card.ToShortString() + ", ";
                    }

                    desc = desc.Remove(desc.Length - 2, 2);
                }
                else
                {
                    desc += "none";
                }
            }

            return desc;
        }
    }
}
