using System;

namespace MgAl2O4.Utils
{
    public class MLUtils
    {
        public static void CalcNetworkLayer(float[] input, float[] output, float[] weights, float[] biases)
        {
            for (int idxO = 0; idxO < output.Length; idxO++)
            {
                output[idxO] = biases[idxO];
                for (int idxI = 0; idxI < input.Length; idxI++)
                {
                    int midx = (idxI * output.Length) + idxO;
                    output[idxO] += input[idxI] * weights[midx];
                }
            }
        }

        public static void ApplySigmoid(float[] arr)
        {
            for (int idx = 0; idx < arr.Length; idx++)
            {
                arr[idx] = (float)(1 / (1 + Math.Exp(-arr[idx])));
            }
        }

        public static void ApplyRelu(float[] arr)
        {
            for (int idx = 0; idx < arr.Length; idx++)
            {
                if (arr[idx] < 0)
                {
                    arr[idx] = 0.0f;
                }
            }
        }

        public static void ApplySoftmax(float[] arr)
        {
            float maxV = arr[0];
            for (int idx = 1; idx < arr.Length; idx++)
            {
                if (maxV < arr[idx])
                {
                    maxV = arr[idx];
                }
            }

            float sumV = 0.0f;
            for (int idx = 0; idx < arr.Length; idx++)
            {
                arr[idx] = (float)Math.Exp(arr[idx] - maxV);
                sumV += arr[idx];
            }

            if (sumV == 0.0) { sumV = 0.001f; }
            for (int idx = 0; idx < arr.Length; idx++)
            {
                arr[idx] /= sumV;
            }
        }

        public static int PickHighestProbability(float[] arr, out float Pct)
        {
            Pct = arr[0];
            int bestIdx = 0;

            for (int idx = 1; idx < arr.Length; idx++)
            {
                if (Pct < arr[idx])
                {
                    Pct = arr[idx];
                    bestIdx = idx;
                }
            }

            return bestIdx;
        }
    }
}
