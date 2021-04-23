/*
 * Ported from: https://github.com/trendmicro/tlsh
 * Source: https://github.com/morganabel/TlshSharp/tree/master/TLSHSharp
 */

/*
 * TLSH is provided for use under two licenses: Apache OR BSD.
 * Users may opt to use either license depending on the license
 * restictions of the systems with which they plan to integrate
 * the TLSH code.
 */

/* ==============
 * Apache License
 * ==============
 * Copyright 2017 Trend Micro Incorporated
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/* ===========
 * BSD License
 * ===========
 * Copyright (c) 2017, Trend Micro Incorporated
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice, this
 *    list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 * 3. Neither the name of the copyright holder nor the names of its contributors
 *    may be used to endorse or promote products derived from this software without
 *    specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
 * INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
 * OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Threading.Tasks;

namespace Palit.TLSHSharp
{
    public class TlshBuilder
    {
        private const int CSlidingWindowSize = 5;
        private const int CBuckets = 256;

        /// <summary>
        /// Minimum length of input accepted by non-forced TLSH hash.
        /// </summary>
        private const int CMinDataLength = 256;

        /// <summary>
        /// Absolute minimum length of input accepted.
        /// </summary>
        private const int CMinForceDataLength = 50;

        private readonly uint[] accumulatorBuckets;
        private readonly int[] slideWindow;
        private int dataLength;

        private readonly int bucketCount;
        private readonly int checksumLength;
        private int checksum;
        private readonly int[] checksumArray = null;
        private readonly int codeSize;

        public TlshBuilder() : this(BucketSize.Buckets128, ChecksumSize.Checksum1Byte) { }

        public TlshBuilder(BucketSize bucketSize, ChecksumSize checksumSize)
        {
            bucketCount = (int)bucketSize;
            checksumLength = (int)checksumSize;

            // Each bucket => 2 bits of output code.
            codeSize = bucketCount >> 2;

            slideWindow = new int[CSlidingWindowSize];
            accumulatorBuckets = new uint[CBuckets];

            if (checksumLength > 1)
            {
                checksumArray = new int[checksumLength];
            }
        }

        public void LoadFromString(string input)
        {
            var buffer = new byte[1024];
            using (var s = TlshUtilities.GenerateStreamFromString(input))
            {
                var bytesRead = s.Read(buffer, 0, buffer.Length);
                while (bytesRead > 0)
                {
                    Update(buffer, 0, bytesRead);
                    bytesRead = s.Read(buffer, 0, buffer.Length);
                }
            }
        }

        public async Task LoadFromStringAsync(string input)
        {
            var buffer = new byte[1024];
            using (var s = TlshUtilities.GenerateStreamFromString(input))
            {
                var bytesRead = await s.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                while (bytesRead > 0)
                {
                    Update(buffer, 0, bytesRead);
                    bytesRead = await s.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                }
            }
        }

        public void Update(byte[] data)
        {
            Update(data, 0, data.Length);
        }

        public void Update(byte[] data, int offset, int byteArrDataLength)
        {
            const int RNG_SIZE = CSlidingWindowSize;

            // Indexes into the sliding window. They cycle like
            // 0 4 3 2 1
            // 1 0 4 3 2
            // 2 1 0 4 3
            // 3 2 1 0 4
            // 4 3 2 1 0
            // 0 4 3 2 1
            // and so on
            int j = dataLength % RNG_SIZE;
            int j_1 = (j - 1 + RNG_SIZE) % RNG_SIZE;
            int j_2 = (j - 2 + RNG_SIZE) % RNG_SIZE;
            int j_3 = (j - 3 + RNG_SIZE) % RNG_SIZE;
            int j_4 = (j - 4 + RNG_SIZE) % RNG_SIZE;

            int fedLength = dataLength;

            for (int i = offset; i < offset + byteArrDataLength; i++, fedLength++)
            {
                slideWindow[j] = data[i] & 0xFF;

                if (fedLength >= 4)
                {
                    // only calculate when input >= 5 bytes

                    checksum = TlshUtilities.PearsonHash(0, slideWindow[j], slideWindow[j_1], checksum);
                    if (checksumLength > 1)
                    {
                        checksumArray[0] = checksum;
                        for (int k = 1; k < checksumLength; k++)
                        {
                            // use calculated 1 byte checksums to expand the total checksum to 3 bytes
                            checksumArray[k] = TlshUtilities.PearsonHash(checksumArray[k - 1], slideWindow[j],
                                    slideWindow[j_1], checksumArray[k]);
                        }
                    }

                    int r;
                    r = TlshUtilities.PearsonHash(2, slideWindow[j], slideWindow[j_1], slideWindow[j_2]);
                    accumulatorBuckets[r]++;
                    r = TlshUtilities.PearsonHash(3, slideWindow[j], slideWindow[j_1], slideWindow[j_3]);
                    accumulatorBuckets[r]++;
                    r = TlshUtilities.PearsonHash(5, slideWindow[j], slideWindow[j_2], slideWindow[j_3]);
                    accumulatorBuckets[r]++;
                    r = TlshUtilities.PearsonHash(7, slideWindow[j], slideWindow[j_2], slideWindow[j_4]);
                    accumulatorBuckets[r]++;
                    r = TlshUtilities.PearsonHash(11, slideWindow[j], slideWindow[j_1], slideWindow[j_4]);
                    accumulatorBuckets[r]++;
                    r = TlshUtilities.PearsonHash(13, slideWindow[j], slideWindow[j_3], slideWindow[j_4]);
                    accumulatorBuckets[r]++;
                }
                // rotate the sliding window indexes
                int j_tmp = j_4;
                j_4 = j_3;
                j_3 = j_2;
                j_2 = j_1;
                j_1 = j;
                j = j_tmp;
            }
            dataLength += byteArrDataLength;
        }

        private uint[] FindQuartiles()
        {
            var bucketCopy = new uint[bucketCount];
            Array.Copy(accumulatorBuckets, bucketCopy, bucketCount);
            var quartile = bucketCount >> 2;
            var p1 = quartile - 1;
            var p2 = p1 + quartile;
            var p3 = p2 + quartile;
            var end = p3 + quartile;

            Array.Sort(bucketCopy);

            return new[] { bucketCopy[p1], bucketCopy[p2], bucketCopy[p3] };
        }

        public TlshHash GetHash(bool force)
        {
            if (!IsValid(force))
            {
                throw new InvalidOperationException("TLSH not valid. Either not enough data or data has too little variance");
            }

            uint q1, q2, q3;
            uint[] quartiles = FindQuartiles();
            q1 = quartiles[0];
            q2 = quartiles[1];
            q3 = quartiles[2];

            var tmp_code = new int[codeSize];
            for (int i = 0; i < codeSize; i++)
            {
                int h = 0;
                for (int j = 0; j < 4; j++)
                {
                    var k = accumulatorBuckets[4 * i + j];
                    if (q3 < k)
                    {
                        h += 3 << (j * 2);
                    }
                    else if (q2 < k)
                    {
                        h += 2 << (j * 2);
                    }
                    else if (q1 < k)
                    {
                        h += 1 << (j * 2);
                    }
                }
                tmp_code[i] = h;
            }

            int lvalue = TlshUtilities.LengthCapture(dataLength);
            int q1ratio = (int)((q1 * 100.0f) / q3) & 0xF;
            int q2ratio = (int)((q2 * 100.0f) / q3) & 0xF;

            if (checksumLength == 1)
            {
                return new TlshHash(new int[] { checksum }, lvalue, q1ratio, q2ratio, tmp_code);
            }
            else
            {
                var checksumArrayCopy = new int[checksumArray.Length];
                Array.Copy(checksumArray, checksumArrayCopy, checksumArray.Length);
                return new TlshHash(checksumArrayCopy, lvalue, q1ratio, q2ratio, tmp_code);
            }
        }

        /// <summary>
        /// Resets the TlshBuilder so it can process another input.
        /// </summary>
        public void Reset()
        {
            Array.Clear(accumulatorBuckets, 0, accumulatorBuckets.Length);
            Array.Clear(slideWindow, 0, slideWindow.Length);
            checksum = 0;
            if (null != checksumArray)
            {
                Array.Clear(checksumArray, 0, checksumArray.Length);
            }
            dataLength = 0;
        }

        /// <summary>
        /// Determines if enough data has been processed by TlshBuilder to produce valid output hash.
        /// </summary>
        /// <param name="force"></param>
        /// <returns></returns>
        public bool IsValid(bool force)
        {
            // Quick return false if length not right.
            if (dataLength < CMinForceDataLength || (!force && dataLength < CMinDataLength))
            {
                return false;
            }

            // >=50% of buckets must be set to be valid.
            var nonZeroBucketCount = 0;
            foreach (var bucket in accumulatorBuckets)
            {
                if (bucket > 0) nonZeroBucketCount++;

                // Exit early when bucket count found to be high enough.
                if (nonZeroBucketCount >= (bucketCount >> 1)) return true;
            }

            return false;
        }
    }
}