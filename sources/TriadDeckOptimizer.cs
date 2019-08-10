using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
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
        private int numGamesToPlay;
        private int numRareToBuild;
        private int numCommonToBuild;
        private int[][] permutationList;
        private bool bUseScoredBuilder;
        private bool bAbort;

        public delegate void FoundDeckDelegate(TriadDeck deck);
        public event FoundDeckDelegate OnFoundDeck;

        private float scoreAvgSides;
        private float scoreStdSides;
        private float scoreSameCorners;
        private float scoreMaxCorner;
        private float scoreRarity;

        public TriadDeckOptimizer()
        {
            numGamesToPlay = 2000;
            numRareToBuild = 10;
            numCommonToBuild = 20;
            bUseScoredBuilder = true;
            bAbort = false;

            scoreAvgSides = 1.0f;
            scoreStdSides = 0.0f;
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

        public void PrepareStats(TriadNpc npc, TriadGameModifier[] regionMods, List<TriadCard> lockedCards, out string numOwnedStr, out string numPossibleStr)
        {
            bool bIsOrderImportant = false;
            foreach (TriadGameModifier mod in npc.Rules)
            {
                bIsOrderImportant = bIsOrderImportant || mod.IsDeckOrderImportant();
            }
            foreach (TriadGameModifier mod in regionMods)
            {
                bIsOrderImportant = bIsOrderImportant || mod.IsDeckOrderImportant();
            }

            //TriadCardDB playerDB = TriadCardDB.Get();
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();

            int numOwned = playerDB.ownedCards.Count;
            numOwnedStr = numOwned.ToString();

            if (bUseScoredBuilder)
            {
                UpdatePossibleDeckCount(numRareToBuild, numCommonToBuild, bIsOrderImportant, lockedCards);
            }
            else
            {
                UpdatePossibleDeckCount(numOwned, bIsOrderImportant);
            }

            numPossibleStr = numPossibleDecks.ToString("N0");
        }

        public Task Process(TriadNpc npc, TriadGameModifier[] regionMods, List<TriadCard> lockedCards)
        {
            this.npc = npc;
            numTestedDecks = 0;
            numMsElapsed = 0;
            bAbort = false;

            return Task.Run(() => {
                if (bUseScoredBuilder)
                {
                    FindDecksScored(regionMods, lockedCards);
                }
                else
                {
                    FindDecks(regionMods);
                }
            });
        }

        public void AbortProcess()
        {
            bAbort = true;
        }

        public bool IsAborted()
        {
            return bAbort;
        }

        private void UpdatePossibleDeckCount(int numOwned, bool bIsOrderImportant)
        {
            // num combinations = numAll! / (5! * (numAll - 5)!)
            numPossibleDecks = 1;
            for (int Idx = 0; Idx < 5; Idx++)
            {
                numPossibleDecks *= (numOwned - Idx);
            }

            int Fact5 = (5 * 4 * 3 * 2 * 1);
            //if (!bIsOrderImportant)   - TEMPORARY, removing permutation checks for Order rule, needs way faster solver / smarter initial set to be viable
            {
                numPossibleDecks /= Fact5;
            }
        }

        private void UpdatePossibleDeckCount(int numRare, int numCommon, bool bIsOrderImportant, List<TriadCard> lockedCards)
        {
            // num possible decks: numRare * (num 4 element combinations from numCommon set)
            // num 4 elem: numCommon! / (4! * (numCommon - 4)!)

            numPossibleDecks = numRare;
            for (int Idx = 0; Idx < 4; Idx++)
            {
                numPossibleDecks *= (numCommon - Idx);
            }

            int Fact4 = (4 * 3 * 2 * 1);
            //if (!bIsOrderImportant)   - TEMPORARY, removing permutation checks for Order rule, needs way faster solver / smarter initial set to be viable
            {
                numPossibleDecks /= Fact4;
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

            public CardScoreData(TriadCard card, float score)
            {
                this.card = card;
                this.score = score;
            }

            public int CompareTo(CardScoreData other)
            {
                return -score.CompareTo(other.score);
            }

            public override string ToString()
            {
                return card.ToShortString() + ", score: " + score;
            }
        }

        private void FindCardsToUse(List<TriadCard> allCards, List<TriadCard> lockedCards, List<TriadGameModifier> modifiers, List<TriadCard> rareList, List<TriadCard> commonList)
        {
            ETriadCardRarity limitedRarity = (allCards.Count < 30) ? ETriadCardRarity.Uncommon : (allCards.Count < 60) ? ETriadCardRarity.Rare : ETriadCardRarity.Epic;
            List<CardScoreData> rareScoredList = new List<CardScoreData>();
            List<CardScoreData> commonScoredList = new List<CardScoreData>();

            foreach (TriadCard card in allCards)
            {
                if (card == null || !card.IsValid()) { continue; }

                // try to guess how good card will perform
                // - avg of sides
                // - std of sides
                // - rarity (should be reflected by sides already)
                // - corners with same number
                // - max corner number

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

                float cardScore =
                    (numberAvg * scoreAvgSides) +
                    (numberStd * scoreStdSides) +
                    (numCorners * scoreSameCorners) +
                    (cornerNum * scoreMaxCorner) +
                    ((int)card.Rarity * scoreRarity);

                foreach (TriadGameModifier mod in modifiers)
                {
                    mod.OnScoreCard(card, ref cardScore);
                }

                if (card.Rarity >= limitedRarity)
                {
                    rareScoredList.Add(new CardScoreData(card, cardScore));
                }
                else
                {
                    commonScoredList.Add(new CardScoreData(card, cardScore));
                }
            }

            rareScoredList.Sort();
            int maxRareToCopy = Math.Min(numRareToBuild, rareScoredList.Count);
            for (int Idx = 0; Idx < maxRareToCopy; Idx++)
            {
                rareList.Add(rareScoredList[Idx].card);
            }

            commonScoredList.Sort();
            int maxCommonToCopy = Math.Min(numCommonToBuild, commonScoredList.Count);
            for (int Idx = 0; Idx < maxCommonToCopy; Idx++)
            {
                commonList.Add(commonScoredList[Idx].card);
            }
        }

        private void FindDecks(TriadGameModifier[] regionMods)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            //TriadCardDB playerDB = TriadCardDB.Get();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ETriadCardRarity RarityLimitThr = 
                (playerDB.ownedCards.Count < 30) ? ETriadCardRarity.Uncommon : 
                (playerDB.ownedCards.Count < 60) ? ETriadCardRarity.Rare :
                ETriadCardRarity.Epic;

            TriadGameSession solver = new TriadGameSession();
            solver.modifiers.AddRange(npc.Rules);
            solver.modifiers.AddRange(regionMods);
            solver.UpdateSpecialRules();

            bool bIsOrderImportant = false;
            foreach (TriadGameModifier mod in solver.modifiers)
            {
                bIsOrderImportant = bIsOrderImportant || mod.IsDeckOrderImportant();
            }

            object lockOb = new object();
            int bestScore = 0;
            TriadDeck bestDeck = new TriadDeck(PlayerSettingsDB.Get().starterCards);

            Parallel.For(1, playerDB.ownedCards.Count, Idx1 =>
            {
                Parallel.For(Idx1 + 1, playerDB.ownedCards.Count, Idx2 =>
                {
                    int rareCounterLv2 =
                        ((playerDB.ownedCards[Idx1].Rarity >= RarityLimitThr) ? 1 : 0) +
                        ((playerDB.ownedCards[Idx2].Rarity >= RarityLimitThr) ? 1 : 0);
                    if (rareCounterLv2 <= 1)
                    {
                        Parallel.For(Idx2 + 1, playerDB.ownedCards.Count, Idx3 =>
                        {
                            int rareCounterLv3 =
                                ((playerDB.ownedCards[Idx1].Rarity >= RarityLimitThr) ? 1 : 0) +
                                ((playerDB.ownedCards[Idx2].Rarity >= RarityLimitThr) ? 1 : 0) +
                                ((playerDB.ownedCards[Idx3].Rarity >= RarityLimitThr) ? 1 : 0);

                            if (rareCounterLv3 <= 1)
                            {
                                Parallel.For(Idx3 + 1, playerDB.ownedCards.Count, Idx4 =>
                                {
                                    int rareCounterLv4 =
                                        ((playerDB.ownedCards[Idx1].Rarity >= RarityLimitThr) ? 1 : 0) +
                                        ((playerDB.ownedCards[Idx2].Rarity >= RarityLimitThr) ? 1 : 0) +
                                        ((playerDB.ownedCards[Idx3].Rarity >= RarityLimitThr) ? 1 : 0) +
                                        ((playerDB.ownedCards[Idx4].Rarity >= RarityLimitThr) ? 1 : 0);
                                    if (rareCounterLv4 <= 1)
                                    {
                                        for (int Idx5 = Idx4 + 1; Idx5 < playerDB.ownedCards.Count; Idx5++)
                                        {
                                            int rareCounterLv5 = rareCounterLv4 +
                                                ((playerDB.ownedCards[Idx5].Rarity >= RarityLimitThr) ? 1 : 0);
                                            if (rareCounterLv5 <= 1)
                                            {
                                                TriadCard[] testDeckCards = new TriadCard[] { playerDB.ownedCards[Idx1], playerDB.ownedCards[Idx2], playerDB.ownedCards[Idx3], playerDB.ownedCards[Idx4], playerDB.ownedCards[Idx5] };
                                                Random randomGen = GetRandomStream(Idx1, Idx2, Idx3, Idx4, Idx5);

                                                /* TEMPORARY, removing permutation checks for Order rule, needs way faster solver / smarter initial set to be viable
                                                if (bIsOrderImportant)
                                                {
                                                    for (int IdxP = 0; IdxP < permutationList.Length; IdxP++)
                                                    {
                                                        int[] UseOrder = permutationList[IdxP];
                                                        TriadDeck permDeck = new TriadDeck(new TriadCard[] { testDeckCards[UseOrder[0]], testDeckCards[UseOrder[1]], testDeckCards[UseOrder[2]], testDeckCards[UseOrder[3]], testDeckCards[UseOrder[4]] });

                                                        int testScore = GetDeckScore(solver, permDeck, randomGen, 10);
                                                        if (testScore > bestScore)
                                                        {
                                                            lock (lockOb)
                                                            {
                                                                bestScore = testScore;
                                                                bestDeck = permDeck;
                                                            }
                                                        }
                                                    }
                                                }
                                                else */
                                                // 
                                                // HACK: place highest rarity card at slot [1]
                                                //
                                                if (bIsOrderImportant)
                                                {
                                                    int fixedRareSlot = 2;
                                                    for (int TestSlotIdx = 0; TestSlotIdx < testDeckCards.Length; TestSlotIdx++)
                                                    {
                                                        if (testDeckCards[TestSlotIdx].Rarity > testDeckCards[fixedRareSlot].Rarity)
                                                        {
                                                            TriadCard swapOb = testDeckCards[TestSlotIdx];
                                                            testDeckCards[TestSlotIdx] = testDeckCards[fixedRareSlot];
                                                            testDeckCards[fixedRareSlot] = swapOb;
                                                        }
                                                    }
                                                }

                                                {
                                                    TriadDeck testDeck = new TriadDeck(testDeckCards);
                                                    int testScore = GetDeckScore(solver, testDeck, randomGen, 1);
                                                    if (testScore > bestScore)
                                                    {
                                                        lock (lockOb)
                                                        {
                                                            bestScore = testScore;
                                                            bestDeck = testDeck;
                                                        }
                                                    }
                                                }
                                            }

                                            numTestedDecks++;
                                        }
                                    }
                                    else
                                    {
                                        numTestedDecks += playerDB.ownedCards.Count - Idx4;
                                    }
                                });
                            }
                            else
                            {
                                numTestedDecks += (playerDB.ownedCards.Count - Idx3) * (playerDB.ownedCards.Count - Idx3 - 1);
                            }
                        });
                    }
                    else
                    {
                        numTestedDecks += (playerDB.ownedCards.Count - Idx2) * (playerDB.ownedCards.Count - Idx2 - 1) * (playerDB.ownedCards.Count - Idx2 - 2);
                    }
                });
            });

            stopwatch.Stop();
            Logger.WriteLine("Building list of decks: " + stopwatch.ElapsedMilliseconds + "ms, num:" + numPossibleDecks);
            optimizedDeck = bestDeck;
        }

        private void FindDecksScored(TriadGameModifier[] regionMods, List<TriadCard> lockedCards)
        {
            PlayerSettingsDB playerDB = PlayerSettingsDB.Get();
            //TriadCardDB playerDB = TriadCardDB.Get();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            TriadGameSession solver = new TriadGameSession();
            solver.modifiers.AddRange(npc.Rules);
            solver.modifiers.AddRange(regionMods);
            solver.UpdateSpecialRules();

            bool bIsOrderImportant = false;
            foreach (TriadGameModifier mod in solver.modifiers)
            {
                bIsOrderImportant = bIsOrderImportant || mod.IsDeckOrderImportant();
            }

            List<TriadCard> rareList = new List<TriadCard>();
            List<TriadCard> commonList = new List<TriadCard>();
            FindCardsToUse(playerDB.ownedCards, lockedCards, solver.modifiers, rareList, commonList);
            UpdatePossibleDeckCount(rareList.Count, commonList.Count, bIsOrderImportant, lockedCards);

            object lockOb = new object();
            int bestScore = 0;
            TriadDeck bestDeck = new TriadDeck(PlayerSettingsDB.Get().starterCards);

            Parallel.For(0, rareList.Count, IdxR0 =>
            {
                if (!bAbort)
                {
                    Parallel.For(0, commonList.Count, IdxC1 =>
                    {
                        if (!bAbort)
                        {
                            for (int IdxC2 = IdxC1 + 1; IdxC2 < commonList.Count; IdxC2++)
                            {
                                for (int IdxC3 = IdxC2 + 1; IdxC3 < commonList.Count; IdxC3++)
                                {
                                    for (int IdxC4 = IdxC3 + 1; IdxC4 < commonList.Count; IdxC4++)
                                    {
                                        TriadCard[] testDeckCards = new TriadCard[] { rareList[IdxR0], commonList[IdxC1], commonList[IdxC2], commonList[IdxC3], commonList[IdxC4] };
                                        Random randomGen = GetRandomStream(IdxR0, IdxC1, IdxC2, IdxC3, IdxC4);

                                        /* TEMPORARY, removing permutation checks for Order rule, needs way faster solver / smarter initial set to be viable
                                        if (bIsOrderImportant)
                                        {
                                            for (int IdxP = 0; IdxP < permutationList.Length; IdxP++)
                                            {
                                                int[] UseOrder = permutationList[IdxP];
                                                TriadCard[] permDeckCards = new TriadCard[] { testDeckCards[UseOrder[0]], testDeckCards[UseOrder[1]], testDeckCards[UseOrder[2]], testDeckCards[UseOrder[3]], testDeckCards[UseOrder[4]] };
                                                TriadDeck permDeck = new TriadDeck(permDeckCards);

                                                int testScore = GetDeckScore(solver, permDeck, randomGen, 10);
                                                if (testScore > bestScore)
                                                {
                                                    lock (lockOb)
                                                    {
                                                        bestScore = testScore;
                                                        bestDeck = permDeck;
                                                        OnFoundDeck.Invoke(permDeck);
                                                    }
                                                }
                                            }
                                        }
                                        else*/
                                        // 
                                        // HACK: place highest rarity card at slot [1]
                                        //
                                        if (bIsOrderImportant)
                                        {
                                            testDeckCards = new TriadCard[] { commonList[IdxC1], rareList[IdxR0], commonList[IdxC2], commonList[IdxC3], commonList[IdxC4] };
                                        }

                                        {
                                            TriadDeck testDeck = new TriadDeck(testDeckCards);
                                            int testScore = GetDeckScore(solver, testDeck, randomGen, 1);
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

                                        lock (lockOb)
                                        {
                                            numTestedDecks++;
                                            if (bAbort)
                                            {
                                                IdxC2 = commonList.Count;
                                                IdxC3 = commonList.Count;
                                                IdxC4 = commonList.Count;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
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
            return numTestedDecks.ToString("N0");
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
