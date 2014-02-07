using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Helpers {
    public class CreditCardHelper {
        private static readonly Regex _cardRegex = new Regex(
              "^(?:(?<Visa>4\\d{3})|(?<MasterCard>5[1-5]\\d{2})|(?<Discover>6011)|(?<DinersClub>(?:3[68]\\d{2})|(?:30[0-5]\\d))|(?<Amex>3[47]\\d{2}))([ -]?)(?(DinersClub)(?:\\d{6}\\1\\d{4})|(?(Amex)(?:\\d{6}\\1\\d{5})|(?:\\d{4}\\1\\d{4}\\1\\d{4})))$",
                RegexOptions.Compiled
            );

        public static string GetSecureCardNumber(string cardNumber) {
            if (String.IsNullOrEmpty(cardNumber) || cardNumber.StartsWith("XXXX"))
                return cardNumber;

            cardNumber = cardNumber.Replace(" ", "").Replace("-", "");
            return String.Concat("XXXX", cardNumber.Substring(cardNumber.Length - 4));
        }

        public static bool IsValidNumber(string cardNum) {
            CreditCardType? cardType = GetCardTypeFromNumber(cardNum);
            return IsValidNumber(cardNum, cardType);
        }

        public static bool IsValidNumber(string cardNum, CreditCardType? cardType) {
            return _cardRegex.Match(cardNum).Groups[cardType.ToString()].Success
                && PassesLuhnTest(cardNum);
        }

        public static CreditCardType? GetCardTypeFromNumber(string cardNum) {
            GroupCollection gc = _cardRegex.Match(cardNum).Groups;

            if (gc[CreditCardType.Amex.ToString()].Success)
                return CreditCardType.Amex;
            
            if (gc[CreditCardType.MasterCard.ToString()].Success)
                return CreditCardType.MasterCard;
            
            if (gc[CreditCardType.Visa.ToString()].Success)
                return CreditCardType.Visa;
            
            if (gc[CreditCardType.Discover.ToString()].Success)
                return CreditCardType.Discover;

            if (gc[CreditCardType.DinersClub.ToString()].Success)
                return CreditCardType.DinersClub;
            
            return null;
        }

        private static readonly string[] _visaCards = new[] { "4111111111111111", "4012888888881881", "4007000000027", "4012888818888" };
        private static readonly string[] _masterCards = new[] { "5555555555554444", "5105105105105100", "5424000000000015" };
        private static readonly string[] _amexCards = new[] { "378282246310005", "371449635398431", "378734493671000", "370000000000002" };
        private static readonly string[] _discoverCards = new[] { "6011111111111117", "6011000990139424", "6011000000000012" };
        private static readonly string[] _dinersClubCards = new[] { "30569309025904", "38520000023237", "38000000000006" };
        private static readonly string[] _jcbCards = new[] { "30569309025904", "38520000023237", "38000000000006" };
        private static List<string> _allCards;
        private static Dictionary<CreditCardType, string[]> _allCardsByType;

        public static bool IsTestCardNumber(string cardNumber) {
            if (_allCards == null) {
                _allCards = new List<string>();
                _allCards.AddRange(_visaCards);
                _allCards.AddRange(_masterCards);
                _allCards.AddRange(_amexCards);
                _allCards.AddRange(_discoverCards);
                _allCards.AddRange(_dinersClubCards);
                _allCards.AddRange(_jcbCards);
            }

            cardNumber = cardNumber.Replace("-", "").Replace(" ", "");
            return _allCards.Contains(cardNumber);
        }

        public static string GetRandomCardTestNumber() {
            if (_allCards == null) {
                _allCards = new List<string>();
                _allCards.AddRange(_visaCards);
                _allCards.AddRange(_masterCards);
                _allCards.AddRange(_amexCards);
            }

            return _allCards.TakeRandom(1).First();
        }

        public static string GetRandomCardTestNumber(CreditCardType cardType) {
            if (_allCardsByType == null) {
                _allCardsByType = new Dictionary<CreditCardType, string[]>();
                _allCardsByType.Add(CreditCardType.Visa, _visaCards);
                _allCardsByType.Add(CreditCardType.MasterCard, _masterCards);
                _allCardsByType.Add(CreditCardType.Amex, _amexCards);
                _allCardsByType.Add(CreditCardType.Discover, _discoverCards);
                _allCardsByType.Add(CreditCardType.DinersClub, _dinersClubCards);
                _allCardsByType.Add(CreditCardType.JCB, _jcbCards);
            }

            return _allCardsByType[cardType].TakeRandom(1).First();
        }

        public static bool PassesLuhnTest(string cardNumber) {
            cardNumber = cardNumber.Replace("-", "").Replace(" ", "");

            var digits = new int[cardNumber.Length];
            for (int len = 0; len < cardNumber.Length; len++)
                digits[len] = Int32.Parse(cardNumber.Substring(len, 1));
            
            int sum = 0;
            bool alt = false;
            for (int i = digits.Length - 1; i >= 0; i--) {
                int curDigit = digits[i];
                if (alt) {
                    curDigit *= 2;
                    if (curDigit > 9) {
                        curDigit -= 9;
                    }
                }
                sum += curDigit;
                alt = !alt;
            }

            return sum % 10 == 0;
        }
    }

    public enum CreditCardType : byte {
        Visa = 1,
        MasterCard = 2,
        Amex = 3,
        Discover = 4,
        DinersClub = 5,
        JCB = 6
    }
}
