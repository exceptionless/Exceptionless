using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
#if !EMBEDDED
using CodeSmith.Core.Security;

namespace CodeSmith.Core.Extensions {
    public
#else
namespace Exceptionless.Extensions {
    internal
#endif
    static class HashExtensions
    {
        #region ComputeHash

        /// <summary>Compute hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <param name="algorithm"> </param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ComputeHash(this string input, HashAlgorithm algorithm)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentNullException("input");

            byte[] data = algorithm.ComputeHash(Encoding.Unicode.GetBytes(input));

            return ToHex(data);
        }

        /// <summary>Compute hash on input stream</summary>
        /// <param name="input">The stream to compute hash on.</param>
        /// <param name="algorithm"> </param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ComputeHash(this Stream input, HashAlgorithm algorithm)
        {
            if (input == null)
                throw new ArgumentNullException("input");

#if !SILVERLIGHT
            var stream = new BufferedStream(input, 1024 * 8);
            byte[] data = algorithm.ComputeHash(stream);
            return ToHex(data);
#else
            byte[] data = algorithm.ComputeHash(input);
            return ToHex(data);
#endif
        }

        /// <summary>
        /// Compute hash on byte array
        /// </summary>
        /// <param name="input">The byte array to get hash from.</param>
        /// <param name="algorithm"> </param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ComputeHash(this byte[] input, HashAlgorithm algorithm)
        {
            if (input == null)
                throw new ArgumentNullException("input");

            byte[] data = algorithm.ComputeHash(input);

            return ToHex(data);
        }
        
#if !SILVERLIGHT
        /// <summary>Compute hash on input string</summary>
        /// <param name="file">The file to get hash from.</param>
        /// <param name="algorithm"> </param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ComputeHash(this FileInfo file, HashAlgorithm algorithm)
        {
            using (var stream = new BufferedStream(file.OpenRead(), 1024 * 8))
            {
                return ComputeHash(stream, algorithm);
            }
        }
#endif

        /// <summary>Compute hash on input string</summary>
        /// <param name="buffer">The string to compute hash on.</param>
        /// <param name="algorithm"> </param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ComputeHash(this StringBuilder buffer, HashAlgorithm algorithm)
        {
            return ComputeHash(buffer.ToString(), algorithm);
        }

        #endregion

        #region SHA1

        /// <summary>Compute SHA1 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA1(this string input)
        {
            return ComputeHash(input, new SHA1Managed());
        }

        /// <summary>Compute SHA1 hash on a collection of input string</summary>
        /// <param name="inputs">The collection of strings to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA1(this IEnumerable<string> inputs)
        {
            var builder = new StringBuilder();

            foreach (var input in inputs)
                builder.Append(input);

            return builder.ToString().ToSHA1();
        }

        /// <summary>Compute SHA1 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA1(this Stream input)
        {
            return ComputeHash(input, new SHA1Managed());
        }

        /// <summary>
        /// Compute SHA1 hash on input string
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA1(this byte[] buffer)
        {
            return ComputeHash(buffer, new SHA1Managed());
        }

#if !SILVERLIGHT
        /// <summary>Compute SHA1 hash on input string</summary>
        /// <param name="file">The file to get hash from.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA1(this FileInfo file)
        {
            return ComputeHash(file, new SHA1Managed());
        }
#endif

        /// <summary>Compute SHA1 hash on input string</summary>
        /// <param name="buffer">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA1(this StringBuilder buffer)
        {
            return ComputeHash(buffer, new SHA1Managed());
        }

        #endregion

        #region SHA256

        /// <summary>Compute SHA256 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA256(this string input)
        {
            return ComputeHash(input, new SHA256Managed());
        }

        /// <summary>Compute SHA256 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA256(this Stream input)
        {
            return ComputeHash(input, new SHA256Managed());
        }

        /// <summary>
        /// Compute SHA256 hash on input string
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA256(this byte[] buffer)
        {
            return ComputeHash(buffer, new SHA256Managed());
        }

#if !SILVERLIGHT
        /// <summary>Compute SHA256 hash on input string</summary>
        /// <param name="file">The file to get hash from.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA256(this FileInfo file)
        {
            return ComputeHash(file, new SHA256Managed());
        }
#endif

        /// <summary>Compute SHA256 hash on input string</summary>
        /// <param name="buffer">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA256(this StringBuilder buffer)
        {
            return ComputeHash(buffer, new SHA256Managed());
        }

        #endregion

        #region SHA512

#if !SILVERLIGHT
        /// <summary>Compute SHA512 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA512(this string input)
        {
            return ComputeHash(input, new SHA512Managed());
        }

        /// <summary>Compute SHA512 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA512(this Stream input)
        {
            return ComputeHash(input, new SHA512Managed());
        }

        /// <summary>
        /// Compute SHA512 hash on input string
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA512(this byte[] buffer)
        {
            return ComputeHash(buffer, new SHA512Managed());
        }

        /// <summary>Compute SHA512 hash on input string</summary>
        /// <param name="file">The file to get hash from.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA512(this FileInfo file)
        {
            return ComputeHash(file, new SHA512Managed());
        }

        /// <summary>Compute SHA512 hash on input string</summary>
        /// <param name="buffer">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToSHA512(this StringBuilder buffer)
        {
            return ComputeHash(buffer, new SHA512Managed());
        }
#endif

        #endregion

#if !EMBEDDED
        #region CRC32

        /// <summary>Compute CRC32 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToCRC32(this string input)
        {
            return ComputeHash(input, new Crc32());
        }

        /// <summary>Compute CRC32 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToCRC32(this Stream input)
        {
            return ComputeHash(input, new Crc32());
        }

        /// <summary>
        /// Compute CRC32 hash on input string
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToCRC32(this byte[] buffer)
        {
            return ComputeHash(buffer, new Crc32());
        }

#if !SILVERLIGHT
        /// <summary>Compute CRC32 hash on input string</summary>
        /// <param name="file">The file to get hash from.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToCRC32(this FileInfo file)
        {
            return ComputeHash(file, new Crc32());
        }
#endif

        /// <summary>Compute CRC32 hash on input string</summary>
        /// <param name="buffer">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToCRC32(this StringBuilder buffer)
        {
            return ComputeHash(buffer, new Crc32());
        }

        #endregion
#endif

#if !SILVERLIGHT

        #region MD5

        /// <summary>Compute MD5 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToMD5(this string input)
        {
            return ComputeHash(input, MD5.Create());
        }

        /// <summary>Compute MD5 hash on input string</summary>
        /// <param name="input">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToMD5(this Stream input)
        {
            return ComputeHash(input, MD5.Create());
        }

        /// <summary>
        /// Compute MD5 hash on input string
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToMD5(this byte[] buffer)
        {
            return ComputeHash(buffer, MD5.Create());
        }

        /// <summary>Compute MD5 hash on input string</summary>
        /// <param name="file">The file to get hash from.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToMD5(this FileInfo file)
        {
            return ComputeHash(file, MD5.Create());
        }

        /// <summary>Compute MD5 hash on input string</summary>
        /// <param name="buffer">The string to compute hash on.</param>
        /// <returns>The hash as a hexadecimal String.</returns>
        public static string ToMD5(this StringBuilder buffer)
        {
            return ComputeHash(buffer, MD5.Create());
        }

        #endregion

#endif

        /// <summary>
        /// Converts a byte array to Hexadecimal.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <returns>Hexadecimal string of the byte array.</returns>
        public static string ToHex(this IEnumerable<byte> bytes)
        {
            var sb = new StringBuilder();
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Converts a byte array to Hexadecimal.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <returns>Hexadecimal string of the byte array.</returns>
        public static string ToBase64(this IEnumerable<byte> bytes)
        {
            return Convert.ToBase64String(bytes.ToArray());
        }

        /// <summary>
        /// Converts a hexadecimal string into a byte array.
        /// </summary>
        /// <param name="hex">The hex String.</param>
        /// <returns>A byte array.</returns>
        public static byte[] ToByteArray(this string hex)
        {
            return Enumerable.Range(0, hex.Length).
                   Where(x => 0 == x % 2).
                   Select(x => Convert.ToByte(hex.Substring(x, 2), 16)).
                   ToArray();
        }
    }
}
