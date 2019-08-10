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
using System.Text;

namespace Palit.TLSHSharp
{
    public class TlshHash
    {
        private const int CRangeLValue = 256;
        private const int CRangeQRatio = 16;

        private readonly int[] checksum; // 1 or 3 bytes
        private readonly int Lvalue; // 1 byte
        private readonly int Q1ratio; // 4 bits
        private readonly int Q2ratio; // 4 bits
        private readonly int[] codes; // 32/64 bytes

        public static TlshHash FromTlshStr(string tlshStr)
        {
            int[] checksum = null;
            int[] tmp_code = null;
		    foreach (BucketSize bucketSize in Enum.GetValues(typeof(BucketSize))) {
			    foreach (ChecksumSize checksumOption in Enum.GetValues(typeof(ChecksumSize))) {
				    if (tlshStr.Length == HashStringLength(bucketSize, checksumOption)) {
					    checksum = new int[(int)checksumOption];
					    tmp_code = new int[(int)bucketSize / 4];
				    }
                }
		    }
		    if (checksum == null) {
			    throw new ArgumentException("Invalid hash string, length does not match any known encoding");
		    }

		    var offset = 0;
		    for (int k = 0; k<checksum.Length; k++) {
			    checksum[k] = TlshUtilities.FromHexSwapped(tlshStr, offset);
			    offset += 2;
		    }

		    var Lvalue = TlshUtilities.FromHexSwapped(tlshStr, offset);
            offset += 2;

		    var qRatios = TlshUtilities.FromHex(tlshStr, offset);
            offset += 2;

		    for (int i = 0; i<tmp_code.Length; i++) {
			    // un-reverse the code during encoding
			    tmp_code[tmp_code.Length - i - 1] = TlshUtilities.FromHex(tlshStr, offset);
			    offset += 2;
		    }

		    return new TlshHash(checksum, Lvalue, qRatios >> 4, qRatios & 0xF, tmp_code);
        }

        public TlshHash(int[] checksum, int lvalue, int q1ratio, int q2ratio, int[] codes)
        {
            this.checksum = checksum;
            Lvalue = lvalue;
            Q1ratio = q1ratio;
            Q2ratio = q2ratio;
            this.codes = codes;
        }

        public int TotalDiff(TlshHash otherHash, bool lengthDiff)
        {
		    if (checksum.Length != otherHash.checksum.Length || codes.Length != otherHash.codes.Length) {
			    throw new ArgumentException("Given TLSH structure was created with different options from this hash and cannot be compared");
            }

            var diff = 0;

		    if (lengthDiff) {
			    var ldiff = TlshUtilities.ModDiff(Lvalue, otherHash.Lvalue, CRangeLValue);
			    if (ldiff == 0)
				    diff = 0;
			    else if (ldiff == 1)
				    diff = 1;
			    else
				    diff += ldiff* 12;
		    }

            var q1diff = TlshUtilities.ModDiff(Q1ratio, otherHash.Q1ratio, CRangeQRatio);
		    if (q1diff <= 1)
			    diff += q1diff;
		    else
			    diff += (q1diff - 1) * 12;

		    var q2diff = TlshUtilities.ModDiff(Q2ratio, otherHash.Q2ratio, CRangeQRatio);
		    if (q2diff <= 1)
			    diff += q2diff;
		    else
			    diff += (q2diff - 1) * 12;

		    for (int k = 0; k<checksum.Length; k++) {
			    if (checksum[k] != otherHash.checksum[k]) {
				    diff++;
				    break;
			    }
		    }

		    diff += TlshUtilities.HashDistance(codes, otherHash.codes);

		    return diff;
	    }

        public override string ToString()
        {
            return GetEncoded();
        }

        public string GetEncoded()
        {
            // The C++ code reverses the order of some of the fields before
            // converting to hex, so copy that behaviour.
            var sb = new StringBuilder(HashStringLength());

            for (int k = 0; k < checksum.Length; k++)
            {
                TlshUtilities.ToHexSwapped(checksum[k], sb);
            }
            TlshUtilities.ToHexSwapped(Lvalue, sb);
            TlshUtilities.ToHex(Q1ratio << 4 | Q2ratio, sb);
            for (int i = 0; i < codes.Length; i++)
            {
                // reverse the code during encoding
                TlshUtilities.ToHex(codes[codes.Length - 1 - i], sb);
            }

            return sb.ToString();
        }

        private static int HashStringLength(BucketSize bucketSize, ChecksumSize checksumSize)
        {
            return ((int)bucketSize / 2) + ((int)checksumSize * 2) + 4;
        }

        private int HashStringLength()
        {
            // extra 4 characters come from length and Q1 and Q2 ratio.
            return (codes.Length * 2) + (checksum.Length * 2) + 4;
        }
    }
}