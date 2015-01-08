using System;
using System.Security.Cryptography;
using Exceptionless.Helpers;

namespace Exceptionless.Core.Security
{
    public class PasswordGenerator
    {
        private const int DEFAULT_LENGTH = 10;
        private const int DEFAULT_REQUIRED_SPECIAL_CHARS = 2;
        private const string DEFAULT_ALPHANUMERIC_CHARS = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string DEFAULT_SPECIAL_CHARS = "!@$?_-";

        public PasswordGenerator()
            : this(DEFAULT_LENGTH, DEFAULT_REQUIRED_SPECIAL_CHARS)
        {
        }

        public PasswordGenerator(int length, int requiredSpecialCharacters) : this(length, requiredSpecialCharacters, DEFAULT_ALPHANUMERIC_CHARS, DEFAULT_SPECIAL_CHARS)
        {
        }

        public PasswordGenerator(int length, int requiredSpecialCharacters, string alphaNumericChars, string specialChars)
        {
            if (length < 1 || length > 128)
            {
                throw new ArgumentException("The specified password length is invalid.", "length");
            }

            if (requiredSpecialCharacters > length || requiredSpecialCharacters < 0)
            {
                throw new ArgumentException("The specified number of required non-alphanumeric characters is invalid.", "requiredSpecialCharacters");
            }

            Length = length;
            RequiredSpecialCharacters = requiredSpecialCharacters;
            AllowedAlphaNumericCharacters = alphaNumericChars;
            AllowedSpecialCharacters = specialChars;
        }

        public string AllowedAlphaNumericCharacters { get; set; }
        public string AllowedSpecialCharacters { get; set; }
        public int Length { get; set; }
        public int RequiredSpecialCharacters { get; set; }

        public string Next()
        {
            if (Length < 1 || Length > 128)
            {
                throw new InvalidOperationException("The specified password length is invalid.");
            }

            if (RequiredSpecialCharacters > Length || (RequiredSpecialCharacters < 0))
            {
                throw new InvalidOperationException("The specified number of required non-alphanumeric characters is invalid.");
            }

            string allowedChars = AllowedAlphaNumericCharacters + AllowedSpecialCharacters;
            byte[] data = new byte[Length];
            char[] password = new char[Length];
            new RNGCryptoServiceProvider().GetBytes(data);

            int nonAlphanumericCharacters = 0;

            for (int i = 0; i < Length; i++)
            {
                int num = data[i] % allowedChars.Length;
                if (num > AllowedAlphaNumericCharacters.Length)
                    nonAlphanumericCharacters++;

                password[i] = allowedChars[num];
            }

            if (nonAlphanumericCharacters < RequiredSpecialCharacters)
            {
                for (int i = 0; i < RequiredSpecialCharacters - nonAlphanumericCharacters; i++)
                {
                    int charIndex;
                    do
                    {
                        charIndex = RandomData.Instance.Next(0, Length);
                    }
                    while (!Char.IsLetterOrDigit(password[charIndex]));

                    int num = RandomData.Instance.Next(0, AllowedSpecialCharacters.Length);
                    password[charIndex] = AllowedSpecialCharacters[num];
                }
            }

            return new string(password);
        }

        public static string Generate()
        {
            return Generate(DEFAULT_LENGTH, DEFAULT_REQUIRED_SPECIAL_CHARS);
        }

        public static string Generate(int length, int requiredSpecialChars)
        {
            var generator = new PasswordGenerator(length, requiredSpecialChars);
            return generator.Next();
        }

        #region Pronounceable Password

        private static readonly char[] _vowels = new[] { 'a', 'e', 'i', 'o', 'u' };
        private static readonly char[] _consonants = new[] { 'b', 'c', 'd', 'f', 'g', 'h', 'j', 'k', 'l', 'm', 'n', 'p', 'r', 's', 't', 'v' };
        private static readonly char[] _doubleConsonants = new[] { 'c', 'd', 'f', 'g', 'l', 'm', 'n', 'p', 'r', 's', 't' };

        public static string GeneratePassword(int passwordLength)
        {
            bool wroteConsonant = false;
            int counter = 0;
            var password = new System.Text.StringBuilder();

            for (counter = 0; counter <= passwordLength; counter++)
            {
                if (password.Length > 0 & (wroteConsonant == false) & (RandomData.Instance.Next(100) < 10))
                {
                    password.Append(_doubleConsonants[RandomData.Instance.Next(_doubleConsonants.Length)], 2);
                    counter += 1;
                    wroteConsonant = true;
                }
                else
                {
                    if ((wroteConsonant == false) & (RandomData.Instance.Next(100) < 90))
                    {
                        password.Append(_consonants[RandomData.Instance.Next(_consonants.Length)]);
                        wroteConsonant = true;
                    }
                    else
                    {
                        password.Append(_vowels[RandomData.Instance.Next(_vowels.Length)]);
                        wroteConsonant = false;
                    }
                }
            }

            password.Length = passwordLength;
            return password.ToString();
        }
        
        #endregion
    }
}
