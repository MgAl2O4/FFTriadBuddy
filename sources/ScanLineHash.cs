using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFTriadBuddy
{
    public class ScanLineHash
    {
        public readonly byte[] Counters;

        public ScanLineHash(byte[] counters)
        {
            Counters = new byte[counters.Length];
            Array.Copy(counters, Counters, Counters.Length);
        }

        public int GetDistance(ScanLineHash other)
        {
            int numCountersA = (Counters != null) ? Counters.Length : 0;
            int numCountersB = (other.Counters != null) ? other.Counters.Length : 0;
            int numSharedCounters = Math.Min(numCountersA, numCountersB);
            int numMisingCounters = Math.Max(numCountersA, numCountersB) - numSharedCounters;

            int distance = numMisingCounters * 256;

            for (int Idx = 0; Idx < numSharedCounters; Idx++)
            {
                distance += Math.Abs(Counters[Idx] - other.Counters[Idx]);
            }

            return distance;
        }

        public bool IsValid()
        {
            return Counters != null;
        }

        public override string ToString()
        {
            return Convert.ToBase64String(Counters);
        }

        public static ScanLineHash FromString(string str)
        {
            byte[] counterArr = Convert.FromBase64String(str);
            return new ScanLineHash(counterArr);
        }

        public static ScanLineHash CreateFromImage(byte[] data, int sizeX, int sizeY)
        {
            byte[] counterArr = new byte[sizeX + sizeY];
            for (int IdxX = 0; IdxX < sizeX; IdxX++)
            {
                int numTotal = 0;
                int lineAcc = 0;

                for (int IdxY = 0; IdxY < sizeY; IdxY++)
                {
                    lineAcc += data[IdxX + (IdxY * sizeX)];
                    numTotal++;
                }

                counterArr[IdxX] = (byte)(lineAcc / numTotal);
            }
            for (int IdxY = 0; IdxY < sizeY; IdxY++)
            {
                int numTotal = 0;
                int lineAcc = 0;

                for (int IdxX = 0; IdxX < sizeX; IdxX++)
                {
                    lineAcc += data[IdxX + (IdxY * sizeX)];
                    numTotal++;
                }

                counterArr[IdxY + sizeX] = (byte)(lineAcc / numTotal);
            }

            return new ScanLineHash(counterArr);
        }
    }
}
