using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FFTriadBuddy
{
    [Flags]
    public enum ETriadGameSpecialMod
    {
        None = 0,
        SelectVisible3 = 0x1,
        SelectVisible5 = 0x2,
        RandomizeRule = 0x4,
        RandomizeBlueDeck = 0x8,
        SwapCards = 0x10,
        BlueCardSelection = 0x20,
        IgnoreOwnedCheck = 0x40,
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
        protected LocString LocRuleName;
        protected bool bAllowCombo = false;
        protected bool bIsDeckOrderImportant = false;
        protected bool bHasLastRedReminder = false;
        protected ETriadGameSpecialMod SpecialMod = ETriadGameSpecialMod.None;
        protected EFeature Features = EFeature.None;

        public virtual string GetCodeName() { return RuleName; }
        public virtual string GetLocalizedName() { return LocRuleName.GetLocalized(); }
        public int GetLocalizationId() { return LocRuleName.Id; }
        public virtual bool AllowsCombo() { return bAllowCombo; }
        public virtual bool IsDeckOrderImportant() { return bIsDeckOrderImportant; }
        public virtual ETriadGameSpecialMod GetSpecialRules() { return SpecialMod; }
        public virtual EFeature GetFeatures() { return Features; }
        public virtual bool HasLastRedReminder() { return bHasLastRedReminder; }
        public override string ToString() { return GetCodeName(); }

        public virtual void OnCardPlaced(TriadGameSimulationState gameData, int boardPos) { }
        public virtual void OnCheckCaptureNeis(TriadGameSimulationState gameData, int boardPos, int[] neiPos, List<int> captureList) { }
        public virtual void OnCheckCaptureCardWeights(TriadGameSimulationState gameData, int boardPos, int neiPos, bool isReverseActive, ref int cardNum, ref int neiNum) { }
        public virtual void OnCheckCaptureCardMath(TriadGameSimulationState gameData, int boardPos, int neiPos, int cardNum, int neiNum, ref bool isCaptured) { }
        public virtual void OnPostCaptures(TriadGameSimulationState gameData, int boardPos) { }
        public virtual void OnScreenUpdate(TriadGameSimulationState gameData) { }
        public virtual void OnAllCardsPlaced(TriadGameSimulationState gameData) { }
        public virtual void OnFilterNextCards(TriadGameSimulationState gameData, ref int allowedCardsMask) { }
        public virtual void OnMatchInit() { }
        public virtual void OnScoreCard(TriadCard card, ref float score) { }

        public int CompareTo(TriadGameModifier otherMod)
        {
            if (otherMod != null)
            {
                string locStrA = GetLocalizedName();
                string locStrB = otherMod.GetLocalizedName();
                return locStrA.CompareTo(locStrB);
            }
            return 0;
        }

        public int CompareTo(object obj)
        {
            return CompareTo((TriadGameModifier)obj);
        }

        public virtual TriadGameModifier Clone()
        {
            return (TriadGameModifier)this.MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            var otherMod = obj as TriadGameModifier;
            return (otherMod != null) && (GetLocalizationId() == otherMod.GetLocalizationId());
        }

        public override int GetHashCode()
        {
            return GetLocalizationId();
        }
    }

    public class TriadGameModifierNone : TriadGameModifier
    {
        public TriadGameModifierNone()
        {
            RuleName = "None";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 0);
        }
        // no special logic
    }

    public class TriadGameModifierRoulette : TriadGameModifier
    {
        protected TriadGameModifier RuleInst;

        public TriadGameModifierRoulette()
        {
            RuleName = "Roulette";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 1);
            SpecialMod = ETriadGameSpecialMod.RandomizeRule;
        }

        public override string GetCodeName() { return base.GetCodeName() + (RuleInst != null ? (" (" + RuleInst.GetCodeName() + ")") : ""); }
        public override string GetLocalizedName() { return base.GetLocalizedName() + (RuleInst != null ? (" (" + RuleInst.GetLocalizedName() + ")") : ""); }
        public override bool AllowsCombo() { return (RuleInst != null) ? RuleInst.AllowsCombo() : base.AllowsCombo(); }
        public override bool IsDeckOrderImportant() { return (RuleInst != null) ? RuleInst.IsDeckOrderImportant() : base.IsDeckOrderImportant(); }
        public override ETriadGameSpecialMod GetSpecialRules() { return base.GetSpecialRules() | ((RuleInst != null) ? RuleInst.GetSpecialRules() : ETriadGameSpecialMod.None); }
        public override EFeature GetFeatures() { return (RuleInst != null) ? RuleInst.GetFeatures() : EFeature.None; }
        public override bool HasLastRedReminder() { return (RuleInst != null) ? RuleInst.HasLastRedReminder() : base.HasLastRedReminder(); }

        public override void OnCardPlaced(TriadGameSimulationState gameData, int boardPos)
        {
            if (RuleInst != null) { RuleInst.OnCardPlaced(gameData, boardPos); }
        }

        public override void OnCheckCaptureNeis(TriadGameSimulationState gameData, int boardPos, int[] neiPos, List<int> captureList)
        {
            if (RuleInst != null) { RuleInst.OnCheckCaptureNeis(gameData, boardPos, neiPos, captureList); }
        }

        public override void OnCheckCaptureCardWeights(TriadGameSimulationState gameData, int boardPos, int neiPos, bool isReverseActive, ref int cardNum, ref int neiNum)
        {
            if (RuleInst != null) { RuleInst.OnCheckCaptureCardWeights(gameData, boardPos, neiPos, isReverseActive, ref cardNum, ref neiNum); }
        }

        public override void OnCheckCaptureCardMath(TriadGameSimulationState gameData, int boardPos, int neiPos, int cardNum, int neiNum, ref bool isCaptured)
        {
            if (RuleInst != null) { RuleInst.OnCheckCaptureCardMath(gameData, boardPos, neiPos, cardNum, neiNum, ref isCaptured); }
        }

        public override void OnPostCaptures(TriadGameSimulationState gameData, int boardPos)
        {
            if (RuleInst != null) { RuleInst.OnPostCaptures(gameData, boardPos); }
        }

        public override void OnAllCardsPlaced(TriadGameSimulationState gameData)
        {
            if (RuleInst != null) { RuleInst.OnAllCardsPlaced(gameData); }
        }

        public override void OnFilterNextCards(TriadGameSimulationState gameData, ref int allowedCardsMask)
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

    public class TriadGameModifierAllOpen : TriadGameModifier
    {
        public TriadGameModifierAllOpen()
        {
            RuleName = "All Open";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 2);
            SpecialMod = ETriadGameSpecialMod.SelectVisible5;
        }

        // shared with three open
        public static void StaticMakeKnown(TriadGameSimulationState gameData, List<int> redIndices)
        {
            const int deckSize = 5;

            TriadDeckInstanceManual deckRedEx = gameData.deckRed as TriadDeckInstanceManual;
            if (deckRedEx != null && redIndices.Count <= deckSize)
            {
                if (gameData.bDebugRules)
                {
                    Logger.WriteLine(">> Open:{0}! red indices:{1}", redIndices.Count, string.Join(", ", redIndices));
                }

                TriadDeck redDeckVisible = new TriadDeck(deckRedEx.deck.knownCards, deckRedEx.deck.unknownCardPool);
                for (int idx = 0; idx < redIndices.Count; idx++)
                {
                    int cardIdx = redIndices[idx];
                    if (cardIdx < deckRedEx.deck.knownCards.Count)
                    {
                        // already known, ignore
                    }
                    else
                    {
                        int idxU = cardIdx - deckRedEx.deck.knownCards.Count;
                        var cardOb = deckRedEx.deck.unknownCardPool[idxU];
                        redDeckVisible.knownCards.Add(cardOb);
                        redDeckVisible.unknownCardPool.Remove(cardOb);
                    }
                }

                // safety for impossible state
                for (int idx = 0; (idx < redDeckVisible.knownCards.Count) && (redDeckVisible.knownCards.Count > deckSize); idx++)
                {
                    var cardOb = redDeckVisible.knownCards[idx];
                    int orgIdx = deckRedEx.GetCardIndex(cardOb);
                    if (!redIndices.Contains(orgIdx))
                    {
                        redDeckVisible.knownCards.RemoveAt(idx);
                        idx--;
                    }
                }

                gameData.deckRed = new TriadDeckInstanceManual(redDeckVisible);
            }
        }
    }

    public class TriadGameModifierThreeOpen : TriadGameModifier
    {
        public TriadGameModifierThreeOpen()
        {
            RuleName = "Three Open";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 3);
            SpecialMod = ETriadGameSpecialMod.SelectVisible3;
        }
        // no special logic
    }

    public class TriadGameModifierSuddenDeath : TriadGameModifier
    {
        public TriadGameModifierSuddenDeath()
        {
            RuleName = "Sudden Death";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 4);
            bHasLastRedReminder = true;
            Features = EFeature.AllPlaced;
        }

        public override void OnAllCardsPlaced(TriadGameSimulationState gameData)
        {
            if (gameData.state == ETriadGameState.BlueDraw && gameData.numRestarts < 3)
            {
                // TODO: don't follow this more than once when simulating in solver?
                //       can get stuck in pretty long loops

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
                                redCardsDebug += deckRedEx.deck.knownCards[Idx].Name.GetCodeName() + ":K, ";
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
                                    redCardsDebug += deckRedEx.deck.unknownCardPool[Idx].Name.GetCodeName() + ":U, ";
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
        public TriadGameModifierReverse()
        {
            RuleName = "Reverse";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 5);
            Features = EFeature.CaptureMath;
        }

        public override void OnCheckCaptureCardMath(TriadGameSimulationState gameData, int boardPos, int neiPos, int cardNum, int neiNum, ref bool isCaptured)
        {
            isCaptured = cardNum < neiNum;
        }

        public override void OnScoreCard(TriadCard card, ref float score)
        {
            const float MaxSum = 40.0f;
            int numberSum = card.Sides[0] + card.Sides[1] + card.Sides[2] + card.Sides[3];
            score = 1.0f - (numberSum / MaxSum);
        }
    }

    public class TriadGameModifierFallenAce : TriadGameModifier
    {
        public TriadGameModifierFallenAce()
        {
            RuleName = "Fallen Ace";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 6);
            Features = EFeature.CaptureWeights;
        }

        public override void OnCheckCaptureCardWeights(TriadGameSimulationState gameData, int boardPos, int neiPos, bool isReverseActive, ref int cardNum, ref int neiNum)
        {
            // note: check if cardNum at [boardPos] can capture neiNum at [neiPos]
            // cardNum:1 vs neiNum:A => override weights to force capture
            // cardNum:A vs neiNum:1 => capture, no need to change weights
            //
            // due to asymetry, it needs to know about active reverse rule and swap sides

            if (isReverseActive)
            {
                if ((cardNum == 10) && (neiNum == 1))
                {
                    cardNum = 0;
                }
            }
            else
            {
                if ((cardNum == 1) && (neiNum == 10))
                {
                    neiNum = 0;
                }
            }
        }
    }

    public class TriadGameModifierSame : TriadGameModifier
    {
        public TriadGameModifierSame()
        {
            RuleName = "Same";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 7);
            bAllowCombo = true;
            Features = EFeature.CaptureNei | EFeature.CardPlaced;
        }

        public override void OnCheckCaptureNeis(TriadGameSimulationState gameData, int boardPos, int[] neiPos, List<int> captureList)
        {
            TriadCardInstance checkCard = gameData.board[boardPos];
            int numSame = 0;
            int neiCaptureMask = 0;
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

                        if (neiCard.owner != checkCard.owner)
                        {
                            neiCaptureMask |= (1 << sideIdx);
                        }
                    }
                }
            }

            if (numSame >= 2)
            {
                for (int sideIdx = 0; sideIdx < 4; sideIdx++)
                {
                    int testNeiPos = neiPos[sideIdx];
                    if ((neiCaptureMask & (1 << sideIdx)) != 0)
                    {
                        TriadCardInstance neiCard = gameData.board[testNeiPos];
                        neiCard.owner = checkCard.owner;
                        captureList.Add(testNeiPos);

                        if (gameData.bDebugRules)
                        {
                            Logger.WriteLine(">> " + RuleName + "! [" + testNeiPos + "] " + neiCard.card.Name.GetCodeName() + " => " + neiCard.owner);
                        }
                    }
                }
            }
        }
    }

    public class TriadGameModifierPlus : TriadGameModifier
    {
        public TriadGameModifierPlus()
        {
            RuleName = "Plus";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 8);
            bAllowCombo = true;
            Features = EFeature.CaptureNei | EFeature.CardPlaced;
        }

        public override void OnCheckCaptureNeis(TriadGameSimulationState gameData, int boardPos, int[] neiPos, List<int> captureList)
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
                                            Logger.WriteLine(">> " + RuleName + "! [" + vsNeiPos + "] " + vsCard.card.Name.GetCodeName() + " => " + vsCard.owner);
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
                                Logger.WriteLine(">> " + RuleName + "! [" + testNeiPos + "] " + neiCard.card.Name.GetCodeName() + " => " + neiCard.owner);
                            }
                        }
                    }
                }
            }
        }
    }

    public class TriadGameModifierAscention : TriadGameModifier
    {
        public TriadGameModifierAscention()
        {
            RuleName = "Ascension";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 9);
            Features = EFeature.CardPlaced | EFeature.PostCapture;
        }

        public override void OnCardPlaced(TriadGameSimulationState gameData, int boardPos)
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
                        Logger.WriteLine(">> " + RuleName + "! [" + boardPos + "] " + checkCard.card.Name.GetCodeName() + " is: " + ((scoreMod > 0) ? "+" : "") + scoreMod);
                    }
                }
            }
        }

        public override void OnPostCaptures(TriadGameSimulationState gameData, int boardPos)
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
                            Logger.WriteLine(">> " + RuleName + "! [" + Idx + "] " + otherCard.card.Name.GetCodeName() + " is: " + ((scoreMod > 0) ? "+" : "") + scoreMod);
                        }
                    }
                }
            }
        }

        public override void OnScreenUpdate(TriadGameSimulationState gameData)
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
            const float ScoreMult = 0.8f;
            score *= ScoreMult;

            bool bHasType = card.Type != ETriadCardType.None;
            if (bHasType)
            {
                score += (1.0f - ScoreMult);
            }
        }
    }

    public class TriadGameModifierDescention : TriadGameModifier
    {
        public TriadGameModifierDescention()
        {
            RuleName = "Descension";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 10);
            Features = EFeature.CardPlaced | EFeature.PostCapture;
        }

        public override void OnCardPlaced(TriadGameSimulationState gameData, int boardPos)
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
                        Logger.WriteLine(">> " + RuleName + "! [" + boardPos + "] " + checkCard.card.Name.GetCodeName() + " is: " + ((scoreMod > 0) ? "+" : "") + scoreMod);
                    }
                }
            }
        }

        public override void OnPostCaptures(TriadGameSimulationState gameData, int boardPos)
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
                            Logger.WriteLine(">> " + RuleName + "! [" + Idx + "] " + otherCard.card.Name.GetCodeName() + " is: " + ((scoreMod > 0) ? "+" : "") + scoreMod);
                        }
                    }
                }
            }
        }

        public override void OnScreenUpdate(TriadGameSimulationState gameData)
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
            const float ScoreMult = 0.5f;
            score *= ScoreMult;

            bool bNoType = card.Type == ETriadCardType.None;
            if (bNoType)
            {
                score += (1.0f - ScoreMult);
            }
        }
    }

    public class TriadGameModifierOrder : TriadGameModifier
    {
        public TriadGameModifierOrder()
        {
            RuleName = "Order";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 11);
            bIsDeckOrderImportant = true; Features = EFeature.FilterNext;
        }

        public override void OnFilterNextCards(TriadGameSimulationState gameData, ref int allowedCardsMask)
        {
            if ((gameData.state == ETriadGameState.InProgressBlue) && (allowedCardsMask != 0))
            {
                int firstBlueIdx = gameData.deckBlue.GetFirstAvailableCardFast();
                allowedCardsMask = (firstBlueIdx < 0) ? 0 : (1 << firstBlueIdx);

                if (gameData.bDebugRules)
                {
                    TriadCard firstBlueCard = gameData.deckBlue.GetCard(firstBlueIdx);
                    Logger.WriteLine(">> " + RuleName + "! next card: " + (firstBlueCard != null ? firstBlueCard.Name.GetCodeName() : "none"));
                }
            }
        }
    }

    public class TriadGameModifierChaos : TriadGameModifier
    {
        public TriadGameModifierChaos()
        {
            RuleName = "Chaos";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 12);
            SpecialMod = ETriadGameSpecialMod.BlueCardSelection;
        }

        // special logic, covered by GUI
    }

    public class TriadGameModifierSwap : TriadGameModifier
    {
        public TriadGameModifierSwap()
        {
            RuleName = "Swap";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 13);
            SpecialMod = ETriadGameSpecialMod.SwapCards;
        }

        // special logic, covered by GUI
        public static void StaticSwapCards(TriadGameSimulationState gameData, TriadCard swapFromBlue, int blueSlotIdx, TriadCard swapFromRed, int redSlotIdx)
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
                    Logger.WriteLine(">> " + DummyOb.RuleName + "! blue[" + blueSlotIdx + "]:" + swapFromBlue.Name.GetCodeName() +
                        " <-> red[" + redSlotIdx + (bIsRedFromKnown ? "" : ":Opt") + "]:" + swapFromRed.Name.GetCodeName());
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
        public TriadGameModifierRandom()
        {
            RuleName = "Random";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 14);
            SpecialMod = ETriadGameSpecialMod.RandomizeBlueDeck;
        }

        // special logic, covered by GUI
        public static void StaticRandomized(TriadGameSimulationState gameData)
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
        public TriadGameModifierDraft()
        {
            RuleName = "Draft";
            LocRuleName = LocalizationDB.Get().FindOrAddLocString(ELocStringType.RuleName, 15);
            SpecialMod = ETriadGameSpecialMod.IgnoreOwnedCheck;
        }
        // no special logic
    }

    public class TriadGameModifierDB
    {
        public List<TriadGameModifier> mods;

        private static TriadGameModifierDB instance = new TriadGameModifierDB();
        public static TriadGameModifierDB Get() { return instance; }

        public TriadGameModifierDB()
        {
            mods = new List<TriadGameModifier>();
            foreach (Type type in Assembly.GetAssembly(typeof(TriadGameModifier)).GetTypes())
            {
                if (type.IsSubclassOf(typeof(TriadGameModifier)))
                {
                    TriadGameModifier modOb = (TriadGameModifier)Activator.CreateInstance(type);
                    mods.Add(modOb);
                }
            }

            mods.Sort((a, b) => (a.GetLocalizationId().CompareTo(b.GetLocalizationId())));

            for (int idx = 0; idx < mods.Count; idx++)
            {
                if (mods[idx].GetLocalizationId() != idx)
                {
                    Logger.WriteLine("FAILED to initialize modifiers!");
                    break;
                }
            }
        }
    }
}
