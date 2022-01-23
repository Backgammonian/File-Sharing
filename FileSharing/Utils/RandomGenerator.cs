using System;
using System.Security.Cryptography;

namespace FileSharing.Utils
{
    public class RandomGenerator : IDisposable
    {
        private readonly RNGCryptoServiceProvider _csp;
        private const string _chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private bool _isDisposed;

        public RandomGenerator()
        {
            _csp = new RNGCryptoServiceProvider();
        }

        public int Next(int minValue, int maxExclusiveValue)
        {
            if (minValue == maxExclusiveValue)
            {
                return minValue;
            }

            if (minValue > maxExclusiveValue)
            {
                var t = minValue;
                minValue = maxExclusiveValue;
                maxExclusiveValue = t;
            }

            var diff = (long)maxExclusiveValue - minValue;
            var upperBound = uint.MaxValue / diff * diff;

            uint ui;
            do
            {
                ui = GetRandomUInt();
            }
            while (ui >= upperBound);

            return (int)(minValue + (ui % diff));
        }

        public static string GetRandomString(int length)
        {
            using var rnd = new RandomGenerator();
            var result = "";
            for (var j = 0; j < length; j++)
            {
                result += _chars[rnd.Next(0, _chars.Length - 1)];
            }

            return result;
        }

        private uint GetRandomUInt()
        {
            var randomBytes = GenerateRandomBytes(sizeof(uint));
            return BitConverter.ToUInt32(randomBytes, 0);
        }

        private byte[] GenerateRandomBytes(int bytesNumber)
        {
            var buffer = new byte[bytesNumber];
            _csp.GetBytes(buffer);
            return buffer;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_csp != null)
            {
                _csp.Dispose();
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
