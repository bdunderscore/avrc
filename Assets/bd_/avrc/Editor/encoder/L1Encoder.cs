using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Random = System.Random;

//
// In order to cut down on contact usage, we encode all the signals we need to send as binary codewords, which are then
// packed into signals sent by a series of proximity contacts. Unfortunately, however, due to timing issues with when
// constraints are executed, when one or both players are moving, we can end up with noise resulting in incorrect
// signals being read. To help avoid this issue, this namespace generates a CRC to attach to the symbols, to help detect
// misreads. Further, the receiving logic requires that the signal remain stable for several frames in order to be
// recognized.
//
namespace net.fushizen.avrc.encoder
{
    public class L1EncodingSpec
    {
        private readonly HashSet<string> symbolSet = new HashSet<string>();
        internal List<string> symbols = new List<string>();

        public L1EncodingSpec()
        {
            // Symbol zero is always a special-case 'null' symbol
            symbols.Add("");
            symbolSet.Add("");
        }

        public void Add(string symbol)
        {
            if (symbolSet.Contains(symbol)) throw new Exception("Symbol already exists: " + symbol);

            symbols.Add(symbol);
            symbolSet.Add(symbol);
        }

        public L1Encoding Compile()
        {
            return new L1Encoding(this);
        }
    }

    public class L1Encoding
    {
        private static uint MIN_BITS = 8;

        private static readonly uint GROUP_BITS = 4;

        // CRC-8F/6 - generates 8-bit CRCs
        // This CRC has a HD of 6 at up to four datawords (we limit to two)
        private static readonly uint CRC_GENERATOR = 0x1d9;
        private static readonly uint MAX_BITS = 16;

        public L1Encoding(L1EncodingSpec spec)
        {
            Codes = ImmutableList.Create<uint>().AddRange(GenerateCodes(spec.symbols.Count));

            Symbols = ImmutableList.Create<string>().AddRange(spec.symbols);
            var symbolsCount = Symbols.Count;
            SymbolMap = ImmutableDictionary<string, int>.Empty;

            for (var i = 0; i < symbolsCount; i++) SymbolMap = SymbolMap.Add(Symbols[i], i);
        }

        private ImmutableList<string> Symbols { get; }
        private ImmutableDictionary<string, int> SymbolMap { get; }
        private ImmutableList<uint> Codes { get; }
        private uint Bits { get; }

        internal static List<uint> GenerateCodes(int symbolCount)
        {
            var codewords = new List<uint>();

            var databits = 1;
            var n = symbolCount;
            while (n > 1)
            {
                n >>= 1;
                databits++;
            }

            if (databits > MAX_BITS) throw new Exception("Too many symbols to encode");

            // Round databits up to a multiple of GROUP_BITS
            databits = (int) ((databits + GROUP_BITS - 1) / GROUP_BITS * GROUP_BITS);

            for (uint sym = 0; sym < symbolCount; sym++)
            {
                // Compute CRC-8 of this symbol
                var accum = sym << 8;
                for (var b = 8 + databits; b >= 8; b--)
                    if ((accum & (1 << b)) != 0)
                        accum ^= CRC_GENERATOR << (b - 8);

                accum &= 0xff;

                // Assemble the codeword from the CRC-8 and the data symbol.
                var codeword = (sym << 8) | accum;

                codewords.Add(codeword);
            }

            return codewords;
        }
    }

    internal class L1EncodingTest
    {
        private uint popcount(uint i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }

        [UnityTest]
        public IEnumerator TestHD()
        {
            var pass = true;
            var codes = L1Encoding.GenerateCodes(0xfff);
            var n = codes.Count;
            var rng = new Random();
            for (var i = 0; i < 10_000; i++)
            {
                var r_x = rng.Next(n);
                var r_y = rng.Next(n);
                if (r_x == r_y) continue;

                var hd = popcount(codes[r_x] ^ codes[r_y]);

                if (hd < 4)
                {
                    pass = false;
                    Debug.LogError($"HD({r_x:X2}/{codes[r_x]:X4}, {r_y:X2}/{codes[r_y]:X4}) = {hd}");
                }
            }

            Assert.IsTrue(pass);
            yield return null;
        }
    }
}