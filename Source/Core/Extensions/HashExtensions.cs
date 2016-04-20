using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Exceptionless.Core.Extensions {
    public static class HashExtensions {
        /// <summary>Compute hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <param name="algorithm"> </param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ComputeHash(this string input, HashAlgorithm algorithm) {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentNullException(nameof(input));

            byte[] data = algorithm.ComputeHash(Encoding.Unicode.GetBytes(input));

            return ToHex(data);
        }

        /// <summary>Compute SHA1 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA1(this string input) {
            return ComputeHash(input, new SHA1Managed());
        }

        /// <summary>Compute SHA1 hash on a collection of input string</summary>
        /// <param name="inputs">The collection of strings to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA1(this IEnumerable<string> inputs) {
            var builder = new StringBuilder();

            foreach (var input in inputs)
                builder.Append(input);

            return builder.ToString().ToSHA1();
        }

        /// <summary>Compute SHA256 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA256(this string input) {
            return ComputeHash(input, new SHA256Managed());
        }

        /// <summary>
        /// Converts a byte array to Hexadecimal.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <returns>Hexadecimal string of the byte array.</returns>
        public static string ToHex(this IEnumerable<byte> bytes) {
            var sb = new StringBuilder();
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}