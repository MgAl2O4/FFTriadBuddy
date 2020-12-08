using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    [Flags]
    public enum ETriadGameSpecialMod
    {
        None = 0,
        RandomizeRule = 1,
        RandomizeBlueDeck = 2,
        SwapCards = 4,
        BlueCardSelection = 8,
    }

    public class TriadGameModifier : IComparable
    {
        [Flags]
        public enum EFeature
        {
            None = 0,
            CardPlaced = 1,
            CaptureNei = 2,
            CaptureWeights = 4,
            CaptureMath = 8,
            PostCapture = 16,
            AllPlaced = 32,
            FilterNext = 64,
        }

        protected string RuleName;
        protected bool bAllowCombo = false;
        protected bool bIsDeckOrderImportant = false;
        protected bool bHasLastRedReminder = false;
        protected ETriadGameSpecialMod SpecialMod = ETriadGameSpecialMod.None;
        protected EFeature Features = EFeature.None;

        public virtual string GetName() { return RuleName; }
        public virtual bool AllowsCombo() { return bAllowCombo; }
        public virtual bool IsDeckOrderImportant() { return bIsDeckOrderImportant; }
        public virtual ETriadGameSpecialMod GetSpecialRules() { return SpecialMod; }
        public virtual EFeature GetFeatures() { return Features; }
        public virtual bool HasLastRedReminder() { return bHasLastRedReminder; }
        public override string ToString() { return GetName(); }

        public virtual void OnCardPlaced(TriadGameData gameData, int boardPos) { }
        public virtual void OnCheckCaptureNeis(TriadGameData gameData, int boardPos, int[] neiPos, List<int> captureList) { }
        public virtual void OnCheckCaptureCardWeights(TriadGameData gameData, int boardPos, int neiPos, ref int cardNum, ref int neiNum) { }
        public virtual void OnCheckCaptureCardMath(TriadGameData gameData, int boardPos, int neiPos, int cardNum, int neiNum, ref bool isCaptured) { }
        public virtual void OnPostCaptures(TriadGameData gameData, int boardPos) { }
        public virtual void OnScreenUpdate(TriadGameData gameData) { }
        public virtual void OnAllCardsPlaced(TriadGameData gameData) { }
        public virtual void OnFilterNextCards(TriadGameData gameData, ref int allowedCardsMask) { }
        public virtual void OnMatchInit() { }
        public virtual void OnScoreCard(TriadCard card, ref float score) { }

        public int CompareTo(TriadGameModifier otherMod)
        {
            return (otherMod != null) ? RuleName.CompareTo(otherMod.RuleName) : 0;
        }

        public int CompareTo(object obj)
        {
            return CompareTo((TriadGameModifier)obj);
        }
    }

    public class TriadGameModifierRoulette : TriadGameModifier
    {
        protected TriadGameModifier RuleInst;

        public TriadGameModifierRoulette() { RuleName = "Roulette"; SpecialMod = ETriadGameSpecialMod.RandomizeRule; }

        public override string GetName() { return RuleName + (RuleInst != null ? " (" + RuleInst.GetName() + ")" : ""); }
        public override bool AllowsCombo() { return (RuleInst != null) ? RuleInst.AllowsCombo() : base.AllowsCombo(); }
        public override bool IsDeckOrderImportant() { return (RuleInst != null) ? RuleInst.IsDeckOrderImportant() : base.IsDeckOrderImportant(); }
        public override ETriadGameSpecialMod GetSpecialRules() { return base.GetSpecialRules() | ((RuleInst != null) ? RuleInst.GetSpecialRules() : ETriadGameSpecialMod.None); }
        public override EFeature GetFeatures() { return (RuleInst != null) ? RuleInst.GetFeatures() : EFeature.None; }
        public override bool HasLastRedReminder() { return (RuleInst != null) ? RuleInst.HasLastRedReminder() : base.HasLastRedReminder(); }

        public override void OnCardPlaced(TriadGameData gameData, int boardPos)
        {
            if (RuleInst != null) { RuleInst.OnCardPlaced(gameData, boardPos); }
        }

        public override void OnCheckCaptureNeis(TriadGameData gameData, int boardPos, int[] neiPos, List<int> captureList)
        {
            if (RuleInst != null) { RuleInst.OnCheckCaptureNeis(gameData, boardPos, neiPos, captureList); }
        }

        public override void OnCheckCaptureCardWeights(TriadGameData gameData, int boardPos, int neiPos, ref int cardNum, ref int neiNum)
        {
            if (RuleInst != null) { RuleInst.OnCheckCaptureCardWeights(gameData, boardPos, neiPos, ref cardNum, ref neiNum); }
        }

        public override void OnCheckCaptureCardMath(TriadGameData gameData, int boardPos, int neiPos, int cardNum, int neiNum, ref bool isCaptured)
        {
            if (RuleInst != null) { RuleInst.OnCheckCaptureCardMath(gameData, boardPos, neiPos, cardNum, neiNum, ref isCaptured); }
        }

        public override void OnPostCaptures(TriadGameData gameData, int boardPos)
        {
            if (RuleInst != null) { RuleInst.OnPostCaptures(gameData, boardPos); }
        }

        public override void OnAllCardsPlaced(TriadGameData gameData)
        {
            if (RuleInst != null) { RuleInst.OnAllCardsPlaced(gameData); }
        }

        public override void OnFilterNextCards(TriadGameData gameData, ref int allowedCardsMask)
        {
            if (RuleInst != null) { RuleInst.OnFilterNextCards(gameData, ref allowedCardsMask); }
        }

        public override void OnMatchInit()
        {
            SetRuleInstance(null);
        }

        public void SetRuleInstance(TriadGameModifier RuleInstance)
        {
            RuleInst = RuleInstance;
        }
    }

    public class TriadGameModifierNone : TriadGameModifier
    {
        public TriadGameModifierNone() { RuleName = "None"; }
        // no special logic
    }

    public class TriadGameModifierAllOpen : TriadGameModifier
    {
        public TriadGameModifierAllOpen() { RuleName = "All Open"; }
        // no special logic
    }

    public class TriadGameModifierThreeOpen : TriadGameModifier
    {
        public TriadGameModifierThreeOpen() { RuleName = "Three Open"; }
        // no special logic
    }

    public class TriadGameModifierSuddenDeath : TriadGameModifier
    {
        public TriadGameModifierSuddenDeath() { RuleName = "Sudden Death"; bHasLastRedReminder = true; Features = EFeature.AllPlaced; }

        public override void OnAllCardsPlaced(TriadGameData gameData)
        {
            if (gameData.state == ETriadGameState.BlueDraw && gameData.numRestarts < 3)
            {
                // implement this rule only for manual mode, screen captures get everything automatically
                TriadDeckInstanceManual deckBlueEx = gameData.deckBlue as TriadDeckInstanceManual;
                TriadDeckInstanceManual deckRedEx = gameData.deckRed as TriadDeckInstanceManual;
                if (deckBlueEx != null && deckRedEx != null)
                {
                    List<TriadCard> blueCards = new List<TriadCard>();
                    List<TriadCard> redCards = new List<TriadCard>();
                    List<TriadCard> redUnknownCards = new List<TriadCard>();
                    string redCardsDebug = "";

                    for (int Idx = 0; Idx < gameData.board.Length; Idx++)
                    {
                        if (gameData.board[Idx].owner == ETriadCardOwner.Blue)
                        {
                            blueCards.Add(gameData.board[Idx].card);
                        }
                        else
                        {
                            redCards.Add(gameData.board[Idx].card);
                        }

                        gameData.board[Idx] = null;
                    }

                    if (deckBlueEx.numPlaced < deckRedEx.numPlaced)
                    {
                        // blue has cards on hand, all known
                        for (int Idx = 0; Idx < deckBlueEx.deck.knownCards.Count; Idx++)
                        {
                            bool bIsAvailable = !deckBlueEx.IsPlaced(Idx);
                            if (bIsAvailable)
                            {
                                blueCards.Add(deckBlueEx.deck.knownCards[Idx]);
                                break;
                            }
                        }

                        gameData.state = ETriadGameState.InProgressBlue;
                    }
                    else
                    {
                        // red has cards on hand, check known vs unknown
                        for (int Idx = 0; Idx < deckRedEx.deck.knownCards.Count; Idx++)
                        {
                            bool bIsAvailable = !deckRedEx.IsPlaced(Idx);
                            if (bIsAvailable)
                            {
                                redCards.Add(deckRedEx.deck.knownCards[Idx]);
                                redCardsDebug += deckRedEx.deck.knownCards[Idx].Name + ":K, ";
                                break;
                            }
                        }

                        if (redCards.Count < blueCards.Count)
                        {
                            for (int Idx = 0; Idx < deckRedEx.deck.unknownCardPool.Count; Idx++)
                            {
                                int cardIdx = Idx + deckRedEx.deck.knownCards.Count;
                                bool bIsAvailable = !deckRedEx.IsPlaced(cardIdx);
                                if (bIsAvailable)
                                {
                                    redUnknownCards.Add(deckRedEx.deck.unknownCardPool[Idx]);
                                    redCardsDebug += deckRedEx.deck.unknownCardPool[Idx].Name + ":U, ";
                                }
                            }
                        }

                        gameData.state = ETriadGameState.InProgressRed;
                    }

                    gameData.deckBlue = new TriadDeckInstanceManual(new TriadDeck(blueCards));
                    gameData.deckRed = new TriadDeckInstanceManual(new TriadDeck(redCards, redUnknownCards));
                    gameData.numCardsPlaced = 0;
                    gameData.numRestarts++;

                    for (int Idx = 0; Idx < gameData.typeMods.Length; Idx++)
                    {
                        gameData.typeMods[Idx] = 0;
                    }

                    if (gameData.bDebugRules)
                    {
                        redCardsDebug = (redCardsDebug.Length > 0) ? redCardsDebug.Remove(redCardsDebug.Length - 2, 2) : "(board only)";
                        ETriadCardOwner nextTurnOwner = (gameData.state == ETriadGameState.InProgressBlue) ? ETriadCardOwner.Blue : ETriadCardOwner.Red;
                        Logger.WriteLine(">> " + RuleName + "! next turn:" + nextTurnOwner + ", red:" + redCardsDebug);
                    }
                }
            }
        }
    }

    public class TriadGameModifierReverse : TriadGameModifier
    {
        public TriadGameModifierReverse() { RuleName = "Reverse"; Features = EFeature.CaptureMath; }

        public override void OnCheckCaptureCardMath(TriadGameData gameData, int boardPos, int neiPos, int cardNum, int neiNum, ref bool isCaptured)
        {
            isCaptured = cardNum < neiNum;
        }

        public override void OnScoreCard(TriadCard card, ref float score)
        {
            int numberSum = card.Sides[0] + card.Sides[1] + card.Sides[2] + card.Sides[3];
            score = 40 - numberSum;
        }
    }

    public class TriadGameModifierFallenAce : TriadGameModifier
    {
        public TriadGameModifierFallenAce() { RuleName = "Fallen Ace"; Features = EFeature.CaptureWeights; }

        public override void OnCheckCaptureCardWeights(TriadGameData gameData, int boardPos, int neiPos, ref int cardNum, ref int neiNum)
        {
            if ((cardNum == 10) && (neiNum == 1))
            {
                cardNum = 0;
            }
            else if ((cardNum == 1) && (neiNum == 10))
            {
                neiNum = 0;
            }
        }
    }

    public class TriadGameModifierSame : TriadGameModifier
    {
        public TriadGameModifierSame() { RuleName = "Same"; bAllowCombo = true; Features = EFeature.CaptureNei | EFeature.CardPlaced; }

        public override void OnCheckCaptureNeis(TriadGameData gameData, int boardPos, int[] neiPos, List<int> captureList)
        {
            TriadCardInstance checkCard = gameData.board[boardPos];
            int numSame = 0;
            for (int sideIdx = 0; sideIdx < 4; sideIdx++)
            {
                int testNeiPos = neiPos[sideIdx];
                if (testNeiPos >= 0 && gameData.board[testNeiPos] != null)
                {
                    TriadCardInstance neiCard = gameData.board[testNeiPos];

                    int numPos = checkCard.GetNumber((ETriadGameSide)sideIdx);
                    int numOther = neiCard.GetOppositeNumber((ETriadGameSide)sideIdx);
                    if (numPos == numOther)
                    {
                        numSame++;
                    }
                }
            }

            if (numSame >= 2)
            {
                for (int sideIdx = 0; sideIdx < 4; sideIdx++)
                {
                    int testNeiPos = neiPos[sideIdx];
                    if (testNeiPos >= 0 && gameData.board[testNeiPos] != null)
                    {
                        TriadCardInstance neiCard = gameData.board[testNeiPos];
                        if (neiCard.owner != checkCard.owner)
                        {
                            neiCard.owner = checkCard.owner;
                            captureList.Add(testNeiPos);

                            if (gameData.bDebugRules)
                            {
                                Logger.WriteLine(">> " + RuleName + "! [" + testNeiPos + "] " + neiCard.card.Name + " => " + neiCard.owner);
                            }
                        }
                    }
                }
            }
        }
    }

    public class TriadGameModifierPlus : TriadGameModifier
    {
        public TriadGameModifierPlus() { RuleName = "Plus"; bAllowCombo = true; Features = EFeature.CaptureNei | EFeature.CardPlaced; }

        public override void OnCheckCaptureNeis(TriadGameData gameData, int boardPos, int[] neiPos, List<int> captureList)
        {
            TriadCardInstance checkCard = gameData.board[boardPos];
            for (int sideIdx = 0; sideIdx < 4; sideIdx++)
            {
                int testNeiPos = neiPos[sideIdx];
                if (testNeiPos >= 0 && gameData.board[testNeiPos] != null)
                {
                    TriadCardInstance neiCard = gameData.board[testNeiPos];
                    if (checkCard.owner != neiCard.owner)
                    {
                        int numPosPattern = checkCard.GetNumber((ETriadGameSide)sideIdx);
                        int numOtherPattern = neiCard.GetOppositeNumber((ETriadGameSide)sideIdx);
                        int sumPattern = numPosPattern + numOtherPattern;
                        bool bIsCaptured = false;

                        for (int vsSideIdx = 0; vsSideIdx < 4; vsSideIdx++)
                        {
                            int vsNeiPos = neiPos[vsSideIdx];
                            if (vsNeiPos >= 0 && sideIdx != vsSideIdx && gameData.board[vsNeiPos] != null)
                            {
                                TriadCardInstance vsCard = gameData.board[vsNeiPos];

                                int numPosVs = checkCard.GetNumber((ETriadGameSide)vsSideIdx);
                                int numOtherVs = vsCard.GetOppositeNumber((ETriadGameSide)vsSideIdx);
                                int sumVs = numPosVs + numOtherVs;

                                if (sumPattern == sumVs)
                                {
                                    bIsCaptured = true;

                                    if (vsCard.owner != checkCard.owner)
                                    {
                                        vsCard.owner = checkCard.owner;
                                        captureList.Add(vsNeiPos);

                                        if (gameData.bDebugRules)
                                        {
                                            Logger.WriteLine(">> " + RuleName + "! [" + vsNeiPos + "] " + vsCard.card.Name + " => " + vsCard.owner);
                                        }
                                    }
                                }
                            }
                        }

                        if (bIsCaptured)
                        {
                            neiCard.owner = checkCard.owner;
                            captureList.Add(testNeiPos);

                            if (gameData.bDebugRules)
                            {
                                Logger.WriteLine(">> " + RuleName + "! [" + testNeiPos + "] " + neiCard.card.Name + " => " + neiCard.owner);
                            }
                        }
                    }
                }
            }
        }
    }

    public class TriadGameModifierAscention : TriadGameModifier
    {
        public TriadGameModifierAscention() { RuleName = "Ascension"; Features = EFeature.CardPlaced | EFeature.PostCapture; }

        public override void OnCardPlaced(TriadGameData gameData, int boardPos)
        {
            TriadCardInstance checkCard = gameData.board[boardPos];
            if (checkCard.card.Type != ETriadCardType.None)
            {
                int scoreMod = gameData.typeMods[(int)checkCard.card.Type];
                if (scoreMod != 0)
                {
                    checkCard.scoreModifier = scoreMod;

                    if (gameData.bDebugRules)
                    {
                        Logger.WriteLine(">> " + RuleName + "! [" + boardPos + "] " + checkCard.card.Name + " is: " + ((scoreMod > 0) ? "+" : "") + scoreMod);
                    }
                }
            }           
        }

        public override void OnPostCaptures(TriadGameData gameData, int boardPos)
        {
            TriadCardInstance checkCard = gameData.board[boardPos];
            if (checkCard.card.Type != ETriadCardType.None)
            {
                int scoreMod = checkCard.scoreModifier + 1;
                gameData.typeMods[(int)checkCard.card.Type] = scoreMod;

                for (int Idx = 0; Idx < gameData.board.Length; Idx++)
                {
                    TriadCardInstance otherCard = gameData.board[Idx];
                    if ((otherCard != null) && (checkCard.card.Type == otherCard.card.Type))
                    {
                        otherCard.scoreModifier = scoreMod;
                        if (gameData.bDebugRules)
                        {
                            Logger.WriteLine(">> " + RuleName + "! [" + Idx + "] " + otherCard.card.Name + " is: " + ((scoreMod > 0) ? "+" : "") + scoreMod);
                        }
                    }
                }
            }
        }

        public override void OnScreenUpdate(TriadGameData gameData)
        {
            for (int Idx = 0; Idx < gameData.typeMods.Length; Idx++)
            {
                gameData.typeMods[Idx] = 0;
            }

            for (int Idx = 0; Idx < gameData.board.Length; Idx++)
            {
                TriadCardInstance checkCard = gameData.board[Idx];
                if (checkCard != null && checkCard.card.Type != ETriadCardType.None)
                {
                    gameData.typeMods[(int)checkCard.card.Type] += 1;
                }
            }

            for (int Idx = 0; Idx < gameData.board.Length; Idx++)
            {
                TriadCardInstance checkCard = gameData.board[Idx];
                if (checkCard != null && checkCard.card.Type != ETriadCardType.None)
                {
                    checkCard.scoreModifier = gameData.typeMods[(int)checkCard.card.Type];
                }
            }
        }

        public override void OnScoreCard(TriadCard card, ref float score)
        {
            bool bHasType = card.Type != ETriadCardType.None;
           if (bHasType)
            {
                score += 10.0f;
            }
        }
    }

    public class TriadGameModifierDescention : TriadGameModifier
    {
        public TriadGameModifierDescention() { RuleName = "Descension"; Features = EFeature.CardPlaced | EFeature.PostCapture; }

        public override void OnCardPlaced(TriadGameData gameData, int boardPos)
        {
            TriadCardInstance checkCard = gameData.board[boardPos];
            if (checkCard.card.Type != ETriadCardType.None)
            {
                int scoreMod = gameData.typeMods[(int)checkCard.card.Type];
                if (scoreMod != 0)
                {
                    checkCard.scoreModifier = scoreMod;

                    if (gameData.bDebugRules)
                    {
                        Logger.WriteLine(">> " + RuleName + "! [" + boardPos + "] " + checkCard.card.Name + " is: " + ((scoreMod > 0) ? "+" : "") + scoreMod);
                    }
                }
            }
        }

        public override void OnPostCaptures(TriadGameData gameData, int boardPos)
        {
            TriadCardInstance checkCard = gameData.board[boardPos];
            if (checkCard.card.Type != ETriadCardType.None)
            {
                int scoreMod = checkCard.scoreModifier - 1;
                gameData.typeMods[(int)checkCard.card.Type] = scoreMod;

                for (int Idx = 0; Idx < gameData.board.Length; Idx++)
                {
                    TriadCardInstance otherCard = gameData.board[Idx];
                    if ((otherCard != null) && (checkCard.card.Type == otherCard.card.Type))
                    {
                        otherCard.scoreModifier = scoreMod;
                        if (gameData.bDebugRules)
                        {
                            Logger.WriteLine(">> " + RuleName + "! [" + Idx + "] " + otherCard.card.Name + " is: " + ((scoreMod > 0) ? "+" : "") + scoreMod);
                        }
                    }
                }
            }
        }

        public override void OnScreenUpdate(TriadGameData gameData)
        {
            for (int Idx = 0; Idx < gameData.typeMods.Length; Idx++)
            {
                gameData.typeMods[Idx] = 0;
            }

            for (int Idx = 0; Idx < gameData.board.Length; Idx++)
            {
                TriadCardInstance checkCard = gameData.board[Idx];
                if (checkCard != null && checkCard.card.Type != ETriadCardType.None)
                {
                    gameData.typeMods[(int)checkCard.card.Type] -= 1;
                }
            }

            for (int Idx = 0; Idx < gameData.board.Length; Idx++)
            {
                TriadCardInstance checkCard = gameData.board[Idx];
                if (checkCard != null && checkCard.card.Type != ETriadCardType.None)
                {
                    checkCard.scoreModifier = gameData.typeMods[(int)checkCard.card.Type];
                }
            }
        }

        public override void OnScoreCard(TriadCard card, ref float score)
        {
            bool bHasType = card.Type != ETriadCardType.None;
            if (bHasType)
            {
                score -= 1000.0f;
            }
        }
    }

    public class TriadGameModifierOrder : TriadGameModifier
    {
        public TriadGameModifierOrder() { RuleName = "Order"; bIsDeckOrderImportant = true; Features = EFeature.FilterNext; }

        public override void OnFilterNextCards(TriadGameData gameData, ref int allowedCardsMask)
        {
            if ((gameData.state == ETriadGameState.InProgressBlue) && (allowedCardsMask != 0))
            {
                int firstBlueIdx = gameData.deckBlue.GetFirstAvailableCardFast();
                allowedCardsMask = (firstBlueIdx < 0) ? 0 : (1 << firstBlueIdx);

                if (gameData.bDebugRules)
                {
                    TriadCard firstBlueCard = gameData.deckBlue.GetCard(firstBlueIdx);
                    Logger.WriteLine(">> " + RuleName + "! next card: " + (firstBlueCard != null ? firstBlueCard.Name : "none"));
                }
            }
        }
    }

    public class TriadGameModifierChaos : TriadGameModifier
    {
        public TriadGameModifierChaos() { RuleName = "Chaos"; SpecialMod = ETriadGameSpecialMod.BlueCardSelection; }

        // special logic, covered by GUI
    }

    public class TriadGameModifierSwap : TriadGameModifier
    {
        public TriadGameModifierSwap() { RuleName = "Swap"; SpecialMod = ETriadGameSpecialMod.SwapCards; }

        // special logic, covered by GUI
        public static void StaticSwapCards(TriadGameData gameData, TriadCard swapFromBlue, int blueSlotIdx, TriadCard swapFromRed, int redSlotIdx)
        {
            // implement this rule only for manual mode, screen captures get everything automatically
            TriadDeckInstanceManual deckBlueEx = gameData.deckBlue as TriadDeckInstanceManual;
            TriadDeckInstanceManual deckRedEx = gameData.deckRed as TriadDeckInstanceManual;
            if (deckBlueEx != null && deckRedEx != null)
            {
                bool bIsRedFromKnown = redSlotIdx < deckRedEx.deck.knownCards.Count;
                if (gameData.bDebugRules)
                {
                    TriadGameModifierSwap DummyOb = new TriadGameModifierSwap();
                    Logger.WriteLine(">> " + DummyOb.RuleName + "! blue[" + blueSlotIdx + "]:" + swapFromBlue.Name +
                        " <-> red[" + redSlotIdx + (bIsRedFromKnown ? "" : ":Opt") + "]:" + swapFromRed.Name);
                }

                TriadDeck blueDeckSwapped = new TriadDeck(deckBlueEx.deck.knownCards, deckBlueEx.deck.unknownCardPool);
                TriadDeck redDeckSwapped = new TriadDeck(deckRedEx.deck.knownCards, deckRedEx.deck.unknownCardPool);

                // ignore order in red deck
                redDeckSwapped.knownCards.Add(swapFromBlue);
                redDeckSwapped.knownCards.Remove(swapFromRed);
                redDeckSwapped.unknownCardPool.Remove(swapFromRed);

                // preserve order in blue deck
                blueDeckSwapped.knownCards[blueSlotIdx] = swapFromRed;

                gameData.deckBlue = new TriadDeckInstanceManual(blueDeckSwapped);
                gameData.deckRed = new TriadDeckInstanceManual(redDeckSwapped);
            }
        }
    }

    public class TriadGameModifierRandom : TriadGameModifier
    {
        public TriadGameModifierRandom() { RuleName = "Random"; SpecialMod = ETriadGameSpecialMod.RandomizeBlueDeck; }

        // special logic, covered by GUI
        public static void StaticRandomized(TriadGameData gameData)
        {
            if (gameData.bDebugRules)
            {
                TriadGameModifierRandom DummyOb = new TriadGameModifierRandom();
                Logger.WriteLine(">> " + DummyOb.RuleName + "! blue deck:" + gameData.deckBlue);
            }
        }
    }

    public class TriadGameModifierDraft : TriadGameModifier
    {
        public TriadGameModifierDraft() { RuleName = "Draft"; }
        
        // no special logic ...yet
    }
}
