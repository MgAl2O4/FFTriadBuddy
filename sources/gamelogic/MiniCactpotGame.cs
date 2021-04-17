using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FFTriadBuddy
{
    public class CactpotNumberHash : IComparable
    {
        public readonly int number;

        public CactpotNumberHash(int number)
        {
            this.number = number;
        }

        public int CompareTo(CactpotNumberHash otherNum)
        {
            return (otherNum != null) ? number.CompareTo(otherNum.number) : 0;
        }

        public int CompareTo(object obj)
        {
            return CompareTo((CactpotNumberHash)obj);
        }

        public override string ToString()
        {
            return number.ToString();
        }
    }

    public class CactpotGame
    {
        public static List<CactpotNumberHash> hashDB;
        private static readonly int[,] cachedSolverData = new int[,] { { 2, 2, 2, 4, 4, 4, 4, 2, 2 }, { 4, 4, 4, 6, 4, 4, 4, 0, 0 }, { 0, 0, 0, 4, 4, 4, 4, 0, 0 }, { 4, 4, 4, 2, 2, 4, 4, 0, 0 }, { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, { 4, 4, 4, 0, 0, 4, 4, 2, 2 }, { 0, 0, 0, 4, 4, 4, 4, 0, 0 }, { 4, 4, 4, 0, 0, 4, 4, 6, 6 }, { 2, 2, 2, 4, 4, 4, 4, 2, 2 } };
        private static readonly int[] payouts = new int[] {
            0, 0, 0, 0, 0, 0, // 0..5 - padding
            10000, // 6
            36, // 7
            720, // 8
            360, // 9
            80, // 10
            252, // 11 
            108, // 12
            72, // 13
            54, // 14
            180, // 15
            72, // 16
            180, // 17
            119, // 18
            36, // 19
            306, // 20
            1080, // 21
            144, // 22
            1800, // 23
            3600, // 24
        };

        public static void InititalizeHashDB()
        {
            hashDB = new List<CactpotNumberHash>();
            for (int Idx = 1; Idx <= 9; Idx++)
            {
                hashDB.Add(new CactpotNumberHash(Idx));
            }
        }

        private static IEnumerable<List<int>> Permutate(List<int> seq, int count)
        {
            if (count == 1)
            {
                yield return seq;
            }
            else
            {
                for (int Idx = 0; Idx < count; Idx++)
                {
                    foreach (var perm in Permutate(seq, count - 1))
                    {
                        yield return perm;
                    }

                    int swap = seq[count - 1];
                    seq.RemoveAt(count - 1);
                    seq.Insert(0, swap);
                }
            }
        }

        private static void CalculateLinePayouts(int[] board, List<int> remainingNumbers, out float[] linePayouts, bool bDebugMode = false)
        {
            // build mapping to combine board and missing number list into fully filled test board
            // val >= 0 : use number from board[val]
            // val < 0  : use number from missingNumbers[val]
            int[] mapBoard = new int[9];
            int nextMissingId = -1;
            for (int Idx = 0; Idx < board.Length; Idx++)
            {
                if (board[Idx] == 0)
                {
                    mapBoard[Idx] = nextMissingId;
                    nextMissingId--;
                }
                else
                {
                    mapBoard[Idx] = Idx;
                }
            }

            // calc avg payouts on each line from all possible permutations
            linePayouts = new float[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
            int[] testBoard = new int[9];

            foreach (List<int> permNumbers in Permutate(remainingNumbers, remainingNumbers.Count))
            {
                for (int Idx = 0; Idx < testBoard.Length; Idx++)
                {
                    testBoard[Idx] = (mapBoard[Idx] < 0) ? permNumbers[-mapBoard[Idx] - 1] : board[Idx];
                }

                // 3 horizontal: 012, 345, 678
                linePayouts[0] += payouts[testBoard[0] + testBoard[1] + testBoard[2]];
                linePayouts[1] += payouts[testBoard[3] + testBoard[4] + testBoard[5]];
                linePayouts[2] += payouts[testBoard[6] + testBoard[7] + testBoard[8]];

                // 3 vertical: 036, 147, 258
                linePayouts[3] += payouts[testBoard[0] + testBoard[3] + testBoard[6]];
                linePayouts[4] += payouts[testBoard[1] + testBoard[4] + testBoard[7]];
                linePayouts[5] += payouts[testBoard[2] + testBoard[5] + testBoard[8]];

                // 2 diagonal: 048, 246
                linePayouts[6] += payouts[testBoard[0] + testBoard[4] + testBoard[8]];
                linePayouts[7] += payouts[testBoard[2] + testBoard[4] + testBoard[6]];
            }

            int permCount = 1;
            for (int Idx = 2; Idx <= remainingNumbers.Count; Idx++)
            {
                permCount *= Idx;
            }

            for (int Idx = 0; Idx < linePayouts.Length; Idx++)
            {
                linePayouts[Idx] /= permCount;
                if (bDebugMode) { Logger.WriteLine("> line[" + Idx + "]: " + linePayouts[Idx]); }
            }
        }

        private static float GetBestScore(int[] board, List<int> remainingNumbers, List<int> remainingPos, out int bestIdx, bool bDebugMode = false)
        {
            bestIdx = 0;
            float bestScore = 0;

            // spot score: explore all possibles results recursively until reaching 4 revealed, avg all best line scores 
            if (remainingPos.Count <= 5)
            {
                CalculateLinePayouts(board, remainingNumbers, out float[] linePayouts);

                for (int Idx = 0; Idx < linePayouts.Length; Idx++)
                {
                    if (bestScore < linePayouts[Idx])
                    {
                        bestScore = linePayouts[Idx];
                        bestIdx = Idx;
                    }
                }

                return bestScore;
            }
            else
            {
                for (int PosIdx = 0; PosIdx < remainingPos.Count; PosIdx++)
                {
                    int useBoardPos = remainingPos[PosIdx];
                    remainingPos.RemoveAt(PosIdx);

                    float sumPos = 0;
                    for (int NumberIdx = 0; NumberIdx < remainingNumbers.Count; NumberIdx++)
                    {
                        int useNumber = remainingNumbers[NumberIdx];
                        remainingNumbers.RemoveAt(NumberIdx);

                        board[useBoardPos] = useNumber;

                        float maxNestedScore = GetBestScore(board, remainingNumbers, remainingPos, out int dummyPos, bDebugMode);
                        sumPos += maxNestedScore;

                        remainingNumbers.Insert(NumberIdx, useNumber);
                    }

                    board[useBoardPos] = 0;
                    remainingPos.Insert(PosIdx, useBoardPos);

                    if (bestScore < sumPos)
                    {
                        bestScore = sumPos;
                        bestIdx = useBoardPos;
                    }
                }

                return bestScore / remainingPos.Count;
            }
        }

        public static int FindNextCircle(int[] board, bool bDebugMode = false)
        {
            // this takes way too long with only 1 known number (~3s to solve)
            // and is acceptable with 2+ (up to 100ms)
            // cache all possibile combinations of first step - call BuildCachedData() on program startup

            Stopwatch timer = new Stopwatch();
            timer.Start();

            int bestIdx = -1;

            List<int> remainingSpots = new List<int>();
            for (int Idx = 0; Idx < board.Length; Idx++)
            {
                if (board[Idx] == 0)
                {
                    remainingSpots.Add(Idx);
                }
                else
                {
                    bestIdx = cachedSolverData[Idx, board[Idx] - 1];
                }
            }

            if (remainingSpots.Count < 8)
            {
                List<int> remainingNumbers = new List<int>();
                for (int Idx = 1; Idx <= 9; Idx++)
                {
                    if (Array.IndexOf(board, Idx) < 0)
                    {
                        remainingNumbers.Add(Idx);
                    }
                }

                GetBestScore(board, remainingNumbers, remainingSpots, out bestIdx, bDebugMode);
            }

            if (bDebugMode) { Logger.WriteLine("> best: " + bestIdx); }

            timer.Stop();
            Logger.WriteLine("FindNextCircle: " + timer.ElapsedMilliseconds + "ms (missing: " + remainingSpots.Count + ")");

            return bestIdx;
        }

        public static void FindBestLine(int[] board, out int fromIdx, out int toIdx, bool bDebugMode = false)
        {
            List<int> remainingNumbers = new List<int>();
            for (int Idx = 1; Idx <= 9; Idx++)
            {
                if (Array.IndexOf(board, Idx) < 0)
                {
                    remainingNumbers.Add(Idx);
                }
            }

            fromIdx = -1;
            toIdx = -1;

            if (remainingNumbers.Count <= 5)
            {
                List<int> dummyList = new List<int>();
                GetBestScore(board, remainingNumbers, dummyList, out int bestLine);

                switch (bestLine)
                {
                    case 0: fromIdx = 0; toIdx = 2; break;
                    case 1: fromIdx = 3; toIdx = 5; break;
                    case 2: fromIdx = 6; toIdx = 8; break;
                    case 3: fromIdx = 0; toIdx = 6; break;
                    case 4: fromIdx = 1; toIdx = 7; break;
                    case 5: fromIdx = 2; toIdx = 8; break;
                    case 6: fromIdx = 0; toIdx = 8; break;
                    case 7: fromIdx = 2; toIdx = 6; break;
                    default: break;
                }
            }
        }

        public static void BuildCachedData()
        {
            int[] board = new int[9] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            string cacheStr = "{ ";

            for (int PosIdx = 0; PosIdx < 9; PosIdx++)
            {
                cacheStr += "{ ";
                for (int NumberIdx = 1; NumberIdx <= 9; NumberIdx++)
                {
                    board[PosIdx] = NumberIdx;

                    int bestCircleIdx = FindNextCircle(board);
                    cacheStr += bestCircleIdx + (NumberIdx < 9 ? ", " : "");
                }

                cacheStr += " }" + (PosIdx < 8 ? ", " : "");
                board[PosIdx] = 0;
            }

            cacheStr += " };";
            Logger.WriteLine("int[,] cachedSolverData = new int[,]" + cacheStr);
        }
    }
}
