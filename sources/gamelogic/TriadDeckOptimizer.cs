﻿using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public class TriadDeckOptimizer
    {
        public TriadDeck optimizedDeck;

        private TriadNpc npc;
        private BigInteger numPossibleDecks;
        private BigInteger numTestedDecks;
        private BigInteger numMsElapsed;

        private float scoreAvgSides;
        private float scoreStdSides;
        private float scoreMaxSides;
        private float scoreSameCorners;
        private float scoreMaxCorner;
        private float scoreRarity;
        private int numGamesToPlay;
        private int numPriorityToBuild;
        private int numCommonToBuild;
        private int numCommonPctToDropPerPriSlot;
        private Dictionary<ETriadCardRarity, int> maxSlotsPerRarity;
        private ETriadCardRarity commonRarity;
        private int[][] permutationList;
        private bool bAbort;
        private bool debugMode;

        public delegate void FoundDeckDelegate(TriadDeck deck);
        public delegate void UpdatePossibleCount(string numPossibleDesc);
        public event FoundDeckDelegate OnFoundDeck;

        private struct CardPool
        {
            public TriadCard[][] priorityLists;
            public TriadCard[] commonList;

            public int[] deckSlotTypes;
        }
        private CardPool currentPool;
        private TriadGameSession currentSolver;
        private bool isOrderImportant;

        private const int DeckSlotCommon = -1;
        private const int DeckSlotLocked = -2;

        public TriadDeckOptimizer()
        {
            numGamesToPlay = 2000;
            numPriorityToBuild = 10;
            numCommonToBuild = 20;
            numCommonPctToDropPerPriSlot = 10;

            maxSlotsPerRarity = new Dictionary<ETriadCardRarity, int>();
            maxSlotsPerRarity.Add(ETriadCardRarity.Legendary, 1);
            maxSlotsPerRarity.Add(ETriadCardRarity.Epic, 2);
            commonRarity = ETriadCardRarity.Rare;

            debugMode = false;
            bAbort = false;
#if DEBUG
            debugMode = true;
#endif // DEBUG

            scoreAvgSides = 1.0f;
            scoreStdSides = 0.0f;
            scoreMaxSides = 0.1f;
            scoreSameCorners = 0.0f;
            scoreMaxCorner = 0.0f;
            scoreRarity = 1.0f;

            // generate lookup for permutations used when deck order is important
            // num entries = 5! = 120
            permutationList = new int[120][];
            int ListIdx = 0;
            for (int IdxP0 = 0; IdxP0 < 5; IdxP0++)
            {
                for (int IdxP1 = 0; IdxP1 < 5; IdxP1++)
                {
                    if (IdxP1 == IdxP0) { continue; }
                    for (int IdxP2 = 0; IdxP2 < 5; IdxP2++)
                    {
                        if (IdxP2 == IdxP0 || IdxP2 == IdxP1) { continue; }
                        for (int IdxP3 = 0; IdxP3 < 5; IdxP3++)
                        {
                            if (IdxP3 == IdxP0 || IdxP3 == IdxP1 || IdxP3 == IdxP2) { continue; }
                            for (int IdxP4 = 0; IdxP4 < 5; IdxP4++)
                            {
                                if (IdxP4 == IdxP0 || IdxP4 == IdxP1 || IdxP4 == IdxP2 || IdxP4 == IdxP3) { continue; }

                                permutationList[ListIdx] = new int[5] { IdxP0, IdxP1, IdxP2, IdxP3, IdxP4 };
                                ListIdx++;
                            }
                        }
                    }
                }
            }
        }

        public void Initialize(TriadNpc npc, TriadGameModifier[] regionMods, List<TriadCard> lockedCards)
        {
            this.npc = npc;
            numPossibleDecks = 1;
            numTestedDecks = 0;
            numMsElapsed = 0;

            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();

            currentSolver = new TriadGameSession();
            currentSolver.modifiers.AddRange(npc.Rules);
            currentSolver.modifiers.AddRange(regionMods);
            currentSolver.UpdateSpecialRules();

            isOrderImportant = false;
            foreach (TriadGameModifier mod in currentSolver.modifiers)
            {
                isOrderImportant = isOrderImportant || mod.IsDeckOrderImportant();
            }

            bool foundCards = FindCardPool(playerDB.ownedCards, currentSolver.modifiers, lockedCards);
            if (foundCards)
            {
                UpdatePossibleDeckCount();
            }
        }

        public Task Process(TriadNpc npc, TriadGameModifier[] regionMods, List<TriadCard> lockedCards)
        {
            this.npc = npc;
            numTestedDecks = 0;
            numMsElapsed = 0;
            bAbort = false;

            return Task.Run(() => { FindDecksScored(regionMods, lockedCards); });
        }

        public void AbortProcess()
        {
            bAbort = true;
        }

        public bool IsAborted()
        {
            return bAbort;
        }

        private void UpdatePossibleDeckCount()
        {
            numPossibleDecks = 1;

            // common slot loops will avoid repeating the same items, include in num iterations
            int numCommonSlots = 0;
            for (int idx = 0; idx < currentPool.deckSlotTypes.Length; idx++)
            {
                int slotType = currentPool.deckSlotTypes[idx];
                if (slotType == DeckSlotCommon)
                {
                    numCommonSlots++;
                }
                else if (slotType >= 0)
                {
                    numPossibleDecks *= currentPool.priorityLists[slotType].Length;
                }
            }

            // num combinations without repetition:
            //  = (pool! / (pool - common)!) / common!
            //  = (pool * (pool - 1) * .. * (pool - common + 1) * (pool - common)! / (pool - common)!) / common!
            //  = (pool * (pool - 1) * .. * (pool - common + 1)) / common!

            if (numCommonSlots > 0)
            {
                int FactNumCommon = 1;
                for (int Idx = 0; Idx < numCommonSlots; Idx++)
                {
                    numPossibleDecks *= (currentPool.commonList.Length - Idx);
                    FactNumCommon *= (Idx + 1);
                }

                numPossibleDecks /= FactNumCommon;
            }
        }

        private Random GetRandomStream(int Idx0, int Idx1, int Idx2, int Idx3, int Idx4)
        {
            int Hash = 13;
            Hash = (Hash * 37) + Idx0;
            Hash = (Hash * 37) + Idx1;
            Hash = (Hash * 37) + Idx2;
            Hash = (Hash * 37) + Idx3;
            Hash = (Hash * 37) + Idx4;

            return new Random(Hash);
        }

        private int GetDeckScore(TriadGameSession solver, TriadDeck testDeck, Random randomGen, int numGamesDiv)
        {
            int deckScore = 0;

            int maxGames = (numGamesToPlay / numGamesDiv) / 2;
            for (int IdxGame = 0; IdxGame < maxGames; IdxGame++)
            {
                TriadGameData gameDataR = solver.StartGame(testDeck, npc.Deck, ETriadGameState.InProgressRed);
                ETriadGameState gameRState = solver.SolverPlayRandomGame(gameDataR, randomGen);
                deckScore += (gameRState == ETriadGameState.BlueWins) ? 2 : (gameRState == ETriadGameState.BlueDraw) ? 1 : 0;

                TriadGameData gameDataB = solver.StartGame(testDeck, npc.Deck, ETriadGameState.InProgressBlue);
                ETriadGameState gameBState = solver.SolverPlayRandomGame(gameDataB, randomGen);
                deckScore += (gameBState == ETriadGameState.BlueWins) ? 2 : (gameBState == ETriadGameState.BlueDraw) ? 1 : 0;
            }

            return deckScore;
        }

        private struct CardScoreData : IComparable<CardScoreData>
        {
            public TriadCard card;
            public float score;

            public int CompareTo(CardScoreData other)
            {
                return -score.CompareTo(other.score);
            }

            public override string ToString()
            {
                return card.ToShortString() + ", score: " + score;
            }
        }

        private void ApplyAscentionFilter(List<CardScoreData> commonScoredList, List<List<CardScoreData>> priScoredList)
        {
            Func<CardScoreData, float> FindCardAscValue = scoredEntry => scoredEntry.score;

            int maxCardTypes = Enum.GetValues(typeof(ETriadCardType)).Length;
            int maxLists = priScoredList.Count + 1;
            List<float>[,] mapCardAscValues = new List<float>[maxCardTypes, maxLists];
            
            for (int idxL = 0; idxL < priScoredList.Count + 1; idxL++)
            {
                for (int idxT = 0; idxT < maxCardTypes; idxT++)
                {
                    mapCardAscValues[idxT, idxL] = new List<float>();
                }

                if (idxL > 0)
                {
                    foreach (var scoredEntry in priScoredList[idxL - 1])
                    {
                        mapCardAscValues[(int)scoredEntry.card.Type, idxL].Add(FindCardAscValue(scoredEntry));
                    }
                }
            }

            foreach (var scoredEntry in commonScoredList)
            {
                if (scoredEntry.card.Type != ETriadCardType.None)
                {
                    mapCardAscValues[(int)scoredEntry.card.Type, 0].Add(FindCardAscValue(scoredEntry));
                }
            }

            ETriadCardType bestType = ETriadCardType.None;
            float bestScore = 0;

            if (debugMode) { Logger.WriteLine("Ascension filter..."); }
            for (int idxT = 0; idxT < maxCardTypes; idxT++)
            {
                if (idxT == (int)ETriadCardType.None)
                {
                    continue;
                }

                float[] typePartialScores = new float[maxLists];
                float typeScore = 0;
                for (int idxL = 0; idxL < maxLists; idxL++)
                {
                    for (int cardIdx = 0; cardIdx < mapCardAscValues[idxT, idxL].Count; cardIdx++)
                    {
                        typePartialScores[idxL] += mapCardAscValues[idxT, idxL][cardIdx];
                    }

                    if (mapCardAscValues[idxT, idxL].Count == 0)
                    {
                        typePartialScores[idxL] = 0;
                    }
                    else
                    {
                        typePartialScores[idxL] /= mapCardAscValues[idxT, idxL].Count;
                    }

                    typeScore += typePartialScores[idxL];
                }
                typeScore /= maxLists;

                if (debugMode) { Logger.WriteLine("  [{0}]: score:{1} ({2})", (ETriadCardType)idxT, typeScore, string.Join(", ", typePartialScores)); }
                if (bestScore <= 0.0f || typeScore > bestScore)
                {
                    bestScore = typeScore;
                    bestType = (ETriadCardType)idxT;
                }
            }

            if (bestType != ETriadCardType.None)
            {
                if (debugMode) { Logger.WriteLine("  best: {0}", bestType); }
                Action<CardScoreData, ETriadCardType> IncreaseScoreForType = (scoredEntry, cardType) =>
                {
                    if (scoredEntry.card.Type == cardType)
                    {
                        scoredEntry.score += 1000.0f;
                    }
                };

                foreach (var scoredEntry in commonScoredList)
                {
                    IncreaseScoreForType(scoredEntry, bestType);
                }

                foreach (var priList in priScoredList)
                {
                    foreach (var scoredEntry in priList)
                    {
                        IncreaseScoreForType(scoredEntry, bestType);
                    }
                }
            }
        }

        private bool FindCardPool(List<TriadCard> allCards, List<TriadGameModifier> modifiers, List<TriadCard> lockedCards)
        {
            currentPool = new CardPool();

            int maxRarityNum = Enum.GetValues(typeof(ETriadCardRarity)).Length;
            int priRarityNum = (int)commonRarity + 1;
            int[] mapAvailRarity = new int[maxRarityNum];

            // special case: don't include any rare slots with reverse rule if there's enough cards in common list
            bool hasReverseMod = false;
            bool hasAscensionMod = false;
            foreach (TriadGameModifier mod in modifiers)
            {
                if (mod.GetType() == typeof(TriadGameModifierReverse))
                {
                    hasReverseMod = true;
                }
                else if (mod.GetType() == typeof(TriadGameModifierAscention))
                {
                    hasAscensionMod = true;
                }
            }

            // find number of priority lists based on unique rarity limits 
            List<ETriadCardRarity> priRarityThr = new List<ETriadCardRarity>();
            for (int idxR = priRarityNum; idxR < maxRarityNum; idxR++)
            {
                ETriadCardRarity testRarity = (ETriadCardRarity)idxR;
                if (!hasReverseMod && maxSlotsPerRarity.ContainsKey(testRarity) && maxSlotsPerRarity[testRarity] > 0)
                {
                    mapAvailRarity[idxR] = maxSlotsPerRarity[testRarity];
                    mapAvailRarity[idxR - 1] -= maxSlotsPerRarity[testRarity];

                    priRarityThr.Add(testRarity);
                }
            }

            if (debugMode) 
            {
                Logger.WriteLine("FindCardPool> priRarityThr:{0}, maxAvail:[{1},{2},{3},{4},{5}], reverse:{6}, ascention:{7}", priRarityThr.Count, 
                    mapAvailRarity[0], mapAvailRarity[1], mapAvailRarity[2], mapAvailRarity[3], mapAvailRarity[4], 
                    hasReverseMod, hasAscensionMod); 
            }

            // check rarity of locked cards, eliminate pri list when threshold is matched
            // when multiple pri rarities are locked, start eliminating from pool above
            // e.g. 2x 4 star locked => 4 star out, 5 star out
            currentPool.deckSlotTypes = new int[lockedCards.Count];
            int numLockedCards = 0;

            for (int idx = 0; idx < lockedCards.Count; idx++)
            {
                TriadCard card = lockedCards[idx];
                if (card != null)
                {
                    if (card.Rarity > commonRarity)
                    {
                        for (int testR = (int)card.Rarity; testR <= maxRarityNum; testR++)
                        {
                            if (mapAvailRarity[testR] > 0)
                            {
                                mapAvailRarity[testR]--;
                                break;
                            }
                        }
                    }

                    currentPool.deckSlotTypes[idx] = DeckSlotLocked;
                    numLockedCards++;
                }
                else
                {
                    currentPool.deckSlotTypes[idx] = DeckSlotCommon;
                }
            }

            if (debugMode) { Logger.WriteLine(">> adjusted for locking, numLocked:{0}, maxAvail:[{1},{2},{3},{4},{5}]", numLockedCards, mapAvailRarity[0], mapAvailRarity[1], mapAvailRarity[2], mapAvailRarity[3], mapAvailRarity[4]); }
            if (numLockedCards == lockedCards.Count)
            {
                return false;
            }

            List<CardScoreData> commonScoredList = new List<CardScoreData>();
            List<List<CardScoreData>> priScoredList = new List<List<CardScoreData>>();
            for (int idxP = 0; idxP < priRarityThr.Count; idxP++)
            {
                priScoredList.Add(new List<CardScoreData>());
            }

            // reverse priority thresholds, idx:0 becomes strongest card
            priRarityThr.Reverse();

            // assign each owned card to scored lists
            foreach (TriadCard card in allCards)
            {
                if (card == null || !card.IsValid()) { continue; }

                // try to guess how good card will perform
                // - avg of sides
                // - std of sides
                // - max of sides
                // - rarity (should be reflected by sides already)
                // - corners with same number
                // - max corner number

                int numberMax = Math.Max(Math.Max(card.Sides[0], card.Sides[1]), Math.Max(card.Sides[2], card.Sides[3]));
                int numberSum = card.Sides[0] + card.Sides[1] + card.Sides[2] + card.Sides[3];
                float numberAvg = numberSum / 4.0f;
                float numberMeanSqDiff =
                    ((card.Sides[0] - numberAvg) * (card.Sides[0] - numberAvg)) +
                    ((card.Sides[1] - numberAvg) * (card.Sides[1] - numberAvg)) +
                    ((card.Sides[2] - numberAvg) * (card.Sides[2] - numberAvg)) +
                    ((card.Sides[3] - numberAvg) * (card.Sides[3] - numberAvg));
                float numberStd = (float)Math.Sqrt(numberMeanSqDiff / 4);

                int cornerNum = 0;
                int numCorners = 0;
                if (card.Sides[0] == card.Sides[1]) { numCorners++; cornerNum = Math.Max(cornerNum, card.Sides[0]); }
                if (card.Sides[1] == card.Sides[2]) { numCorners++; cornerNum = Math.Max(cornerNum, card.Sides[1]); }
                if (card.Sides[2] == card.Sides[3]) { numCorners++; cornerNum = Math.Max(cornerNum, card.Sides[2]); }
                if (card.Sides[3] == card.Sides[0]) { numCorners++; cornerNum = Math.Max(cornerNum, card.Sides[3]); }

                CardScoreData scoredCard = new CardScoreData() { card = card };
                scoredCard.score =
                    (numberAvg * scoreAvgSides) +
                    (numberStd * scoreStdSides) +
                    (numberMax * scoreMaxSides) +
                    (numCorners * scoreSameCorners) +
                    (cornerNum * scoreMaxCorner) +
                    ((int)card.Rarity * scoreRarity);

                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnScoreCard(card, ref scoredCard.score);
                }

                for (int idxP = 0; idxP < priRarityThr.Count; idxP++)
                {
                    if (card.Rarity <= priRarityThr[idxP])
                    {
                        priScoredList[idxP].Add(scoredCard);
                    }
                }

                if (card.Rarity <= commonRarity)
                {
                    commonScoredList.Add(scoredCard);
                }
            }

            if (debugMode) { Logger.WriteLine(">> card lists sorted, common:{0}", commonScoredList.Count); }
            bool isPoolValid = (commonScoredList.Count > 0);
            if (isPoolValid)
            {
                int numPriLists = 0;
                int deckSlotIdx = isOrderImportant ? 1 : 0;

                for (int idx = 0; idx < priScoredList.Count; idx++)
                {
                    int numAvail = mapAvailRarity[(int)priRarityThr[idx]];
                    if (debugMode) { Logger.WriteLine("  pri list[{0}]:{1}, rarity:{2}, avail:{3}", idx, priScoredList[idx].Count, priRarityThr[idx], numAvail); }
                    if ((numAvail > 0) && (priScoredList[idx].Count > 0))
                    {
                        // initial deckSlotIdx should be already past only available spot (e.g. all slots but [0] are locked), make sure to wrap around
                        // find fist available Common slot to overwrite with priority list, repeat numAvail times
                        for (int idxAvail = 0; idxAvail < numAvail; idxAvail++)
                        {
                            for (int idxD = 0; idxD < currentPool.deckSlotTypes.Length; idxD++)
                            {
                                if (currentPool.deckSlotTypes[deckSlotIdx] == DeckSlotCommon)
                                {
                                    break;
                                }

                                deckSlotIdx++;
                            }

                            currentPool.deckSlotTypes[deckSlotIdx] = numPriLists;
                        }

                        numPriLists++;
                    }
                    else
                    {
                        priScoredList[idx].Clear();
                    }
                }

                // ascension modifier special case: same type across all pools is best
                // aply after priority lists were trimmed
                if (hasAscensionMod)
                {
                    ApplyAscentionFilter(commonScoredList, priScoredList);
                }

                if (numPriLists > 0)
                {
                    currentPool.priorityLists = new TriadCard[numPriLists][];
                    if (debugMode) { Logger.WriteLine(">> num priority lists:{0}", numPriLists); }

                    int idxP = 0;
                    for (int idxL = 0; idxL < priScoredList.Count; idxL++)
                    {
                        int maxPriorityToUse = Math.Min(numPriorityToBuild, priScoredList[idxL].Count);
                        if (maxPriorityToUse > 0)
                        {
                            currentPool.priorityLists[idxP] = new TriadCard[maxPriorityToUse];
                            priScoredList[idxL].Sort();

                            for (int idxC = 0; idxC < maxPriorityToUse; idxC++)
                            {
                                currentPool.priorityLists[idxP][idxC] = priScoredList[idxL][idxC].card;
                            }

                            idxP++;
                        }
                    }
                }

                // adjust pool of common cards based on avail common slots
                // - all common: use requested size
                // - scale down 20% per every priority list slot 
                int numPriSlots = 0;
                for (int idx = 0; idx < currentPool.deckSlotTypes.Length; idx++)
                {
                    numPriSlots += (currentPool.deckSlotTypes[idx] >= 0) ? 1 : 0;
                }

                int maxCommonToUse = Math.Min(numCommonToBuild - (numCommonToBuild * numPriSlots * numCommonPctToDropPerPriSlot / 100), commonScoredList.Count);
                if (debugMode) { Logger.WriteLine(">> adjusting common pool based on priSlots:{0} and drop:{1}% => {2}", numPriSlots, numCommonPctToDropPerPriSlot, maxCommonToUse); }

                currentPool.commonList = new TriadCard[maxCommonToUse];
                commonScoredList.Sort();

                for (int idx = 0; idx < currentPool.commonList.Length; idx++)
                {
                    currentPool.commonList[idx] = commonScoredList[idx].card;
                }
            }

            if (debugMode) { Logger.WriteLine(">> deck slot types:[{0}, {1}, {2}, {3}, {4}]", currentPool.deckSlotTypes[0], currentPool.deckSlotTypes[1], currentPool.deckSlotTypes[2], currentPool.deckSlotTypes[3], currentPool.deckSlotTypes[4]); }
            return isPoolValid;
        }

        private void FindDecksScored(TriadGameModifier[] regionMods, List<TriadCard> lockedCards)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            if (currentPool.commonList == null && currentPool.priorityLists == null)
            {
                stopwatch.Stop();
                Logger.WriteLine("Skip deck building, everything was locked");

                optimizedDeck = new TriadDeck(lockedCards);
                return;
            }

            object lockOb = new object();
            int bestScore = 0;
            TriadDeck bestDeck = new TriadDeck(PlayerSettingsDB.Get().starterCards);

            // no more flexible slot count after this point => loop land
            const int numSlots = 5;
            TriadCard[][] slotLists = new TriadCard[numSlots][];
            for (int idx = 0; idx < numSlots; idx++)
            {
                slotLists[idx] =
                    (currentPool.deckSlotTypes[idx] == DeckSlotCommon) ? currentPool.commonList :
                    (currentPool.deckSlotTypes[idx] >= 0) ? currentPool.priorityLists[currentPool.deckSlotTypes[idx]] :
                    new TriadCard[1] { lockedCards[idx] };
            }

            Func<int, int, int, int, int, int> FindLoopStart = (SlotIdx, IdxS0, IdxS1, IdxS2, IdxS3) =>
            {
                if (bAbort) { return slotLists[SlotIdx].Length; }
                if (currentPool.deckSlotTypes[SlotIdx] != DeckSlotCommon) { return 0; }

                if (SlotIdx >= 4 && currentPool.deckSlotTypes[3] == DeckSlotCommon) { return IdxS3 + 1; }
                if (SlotIdx >= 3 && currentPool.deckSlotTypes[2] == DeckSlotCommon) { return IdxS2 + 1; }
                if (SlotIdx >= 2 && currentPool.deckSlotTypes[1] == DeckSlotCommon) { return IdxS1 + 1; }
                if (SlotIdx >= 1 && currentPool.deckSlotTypes[0] == DeckSlotCommon) { return IdxS0 + 1; }

                return 0;
            };

            Parallel.For(0, slotLists[0].Length, IdxS0 =>
            //for (int IdxS0 = 0; IdxS0 < slotLists[0].Length; IdxS0++)
            {
                int startS1 = FindLoopStart(1, IdxS0, -1, -1, -1);
                Parallel.For(startS1, slotLists[1].Length, IdxS1 =>
                //for (int IdxS1 = startS1; IdxS1 < slotLists[1].Length; IdxS1++)
                {
                    int startS2 = FindLoopStart(2, IdxS0, IdxS1, -1, -1);
                    Parallel.For(startS2, slotLists[2].Length, IdxS2 =>
                    //for (int IdxS2 = startS2; IdxS2 < slotLists[2].Length; IdxS2++)
                    {
                        int startS3 = FindLoopStart(3, IdxS0, IdxS1, IdxS2, -1);
                        for (int IdxS3 = startS3; IdxS3 < slotLists[3].Length; IdxS3++)
                        {
                            int startS4 = FindLoopStart(4, IdxS0, IdxS1, IdxS2, IdxS3);
                            for (int IdxS4 = startS4; IdxS4 < slotLists[4].Length; IdxS4++)
                            {
                                TriadCard[] testDeckCards = new TriadCard[] { slotLists[0][IdxS0], slotLists[1][IdxS1], slotLists[2][IdxS2], slotLists[3][IdxS3], slotLists[4][IdxS4] };
                                if (testDeckCards[0] != testDeckCards[1] &&
                                    testDeckCards[0] != testDeckCards[2] &&
                                    testDeckCards[0] != testDeckCards[3] &&
                                    testDeckCards[0] != testDeckCards[4] &&
                                    testDeckCards[1] != testDeckCards[2] &&
                                    testDeckCards[1] != testDeckCards[3] &&
                                    testDeckCards[1] != testDeckCards[4] &&
                                    testDeckCards[2] != testDeckCards[3] &&
                                    testDeckCards[2] != testDeckCards[4] &&
                                    testDeckCards[3] != testDeckCards[4])
                                {
                                    Random randomGen = GetRandomStream(IdxS0, IdxS1, IdxS2, IdxS3, IdxS4);
                                    // TODO: custom permutation lookup
                                    {
                                        TriadDeck testDeck = new TriadDeck(testDeckCards);
                                        int testScore = GetDeckScore(currentSolver, testDeck, randomGen, 1);
                                        if (testScore > bestScore)
                                        {
                                            lock (lockOb)
                                            {
                                                bestScore = testScore;
                                                bestDeck = testDeck;
                                                OnFoundDeck.Invoke(testDeck);
                                            }
                                        }
                                    }
                                }

                                lock (lockOb)
                                {
                                    numTestedDecks++;
                                }
                            }
                        }
                    });
                });
            });

            stopwatch.Stop();
            Logger.WriteLine("Building list of decks: " + stopwatch.ElapsedMilliseconds + "ms, num:" + numPossibleDecks);
            optimizedDeck = bestDeck;
        }

        public int GetProgress()
        {
            if (numPossibleDecks > 0)
            {
                string desc = (100 * numTestedDecks / numPossibleDecks).ToString();
                int progressPct = int.Parse(desc);
                return Math.Max(0, Math.Min(100, progressPct));
            }

            return 0;
        }

        public string GetNumTestedDesc()
        {
            return numTestedDecks.ToString("N0", CultureInfo.InvariantCulture);
        }

        public string GetNumPossibleDecksDesc()
        {
            return numPossibleDecks.ToString("N0", CultureInfo.InvariantCulture);
        }

        public int GetSecondsRemaining(int ElapsedMs)
        {
            int numSeconds = int.MaxValue;
            numMsElapsed += ElapsedMs;

            BigInteger numTestedPerMs = numTestedDecks / numMsElapsed;
            BigInteger numMsPerTest = (numTestedDecks == 0) ? 1 : (numMsElapsed / numTestedDecks);
            BigInteger numTestsRemaning = numPossibleDecks - numTestedDecks;

            BigInteger numSecRemaning = (numTestedPerMs > 0) ?
                ((numTestsRemaning / numTestedPerMs) / 1000) :
                ((numTestsRemaning * numMsPerTest) / 1000);

            string numIntervalsDesc = numSecRemaning.ToString();
            int.TryParse(numIntervalsDesc, out numSeconds);

            return Math.Max(0, numSeconds);
        }
    }
}
