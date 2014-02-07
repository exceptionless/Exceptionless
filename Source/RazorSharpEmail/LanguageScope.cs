using System;
using System.Globalization;

namespace RazorSharpEmail {
    public class LanguageScope : IDisposable {
        private readonly string _previousLanguageForThisScopeInstance;
        private readonly string _languageForThisScopeInstance;

        public static void SetDefaultLanguage(string defaultLanguage) {
            if (defaultLanguage == null) throw new ArgumentNullException("defaultLanguage");
            DefaultLanguage = defaultLanguage;
        }

        public static void ClearDefaultLanguage() {
            DefaultLanguage = null;
        }

        private static string DefaultLanguage { get; set; }

        [ThreadStatic]
        private static string _currentLanguage;

        public static CultureInfo CurrentCulture {
            get { return CurrentLanguage != null ? CultureInfo.GetCultureInfo(CurrentLanguage) : null; }
        }

        public static string CurrentLanguage {
            get { return _currentLanguage ?? DefaultLanguage; }
        }

        public LanguageScope(string language) {
            if (language == null) throw new ArgumentNullException("language");

            _languageForThisScopeInstance = language;
            _previousLanguageForThisScopeInstance = _currentLanguage;
            _currentLanguage = _languageForThisScopeInstance;
        }

        public void Dispose() {
            if (_languageForThisScopeInstance == null) {
                // we weren't initialized properly
                return;
            }

            _currentLanguage = _previousLanguageForThisScopeInstance;
        }
    }
}