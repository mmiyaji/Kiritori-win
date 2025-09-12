using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// 参照: Windows.winmd（Windows 10 SDK）
// using Windows.Globalization;
// using Windows.Media.Ocr;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Kiritori.Services.Ocr
{
    internal static class WindowsOcrHelper
    {
        /// <summary>利用可能な OCR 言語の BCP-47 タグ一覧（例: "ja", "en-US"）</summary>
        public static IReadOnlyList<string> GetAvailableLanguageTags()
        {
            try
            {
                // OcrEngine.AvailableRecognizerLanguages は IReadOnlyList<Language>
                var langs = OcrEngine.AvailableRecognizerLanguages;
                var list = new List<string>(langs.Count);
                foreach (var l in langs)
                {
                    // Language.Tag は BCP-47
                    list.Add(l.LanguageTag);
                }
                return list;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// 希望タグ（null/"auto"可）から最適な BCP-47 タグを決める。
        /// 優先度: exact match → 同じ2文字言語 → CurrentUICulture → "en"/"en-US" → 最初の1件
        /// </summary>
        public static string PickBestLanguageTag(string preferredOrAuto)
        {
            var available = GetAvailableLanguageTags();
            if (available.Count == 0) return null;

            var pref = (preferredOrAuto ?? "auto").Trim();
            if (!string.Equals(pref, "auto", StringComparison.OrdinalIgnoreCase))
            {
                // 1) 完全一致
                var exact = available.FirstOrDefault(t => string.Equals(t, pref, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(exact)) return exact;

                // 2) 2文字言語コード一致
                var prefIso = ToIso(pref);
                var two = available.FirstOrDefault(t => string.Equals(ToIso(t), prefIso, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(two)) return two;
            }

            // 3) CurrentUICulture
            var uiIso = ToIso(CultureInfo.CurrentUICulture.Name);
            var fromUi = available.FirstOrDefault(t => string.Equals(ToIso(t), uiIso, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(fromUi)) return fromUi;

            // 4) 英語
            var en = available.FirstOrDefault(t => string.Equals(ToIso(t), "en", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(en)) return en;

            // 5) 何か1つ
            return available[0];
        }

        /// <summary>言語タグから OCR エンジンを生成（null なら最適なタグを自動選択）。生成に失敗したら null。</summary>
        public static OcrEngine TryCreateEngine(string preferredOrAuto, out string resolvedTag)
        {
            resolvedTag = null;
            try
            {
                var tag = PickBestLanguageTag(preferredOrAuto);
                if (string.IsNullOrEmpty(tag)) return null;

                var lang = new Language(tag);
                var engine = OcrEngine.TryCreateFromLanguage(lang);
                if (engine != null)
                {
                    resolvedTag = lang.LanguageTag; // 実際に作れたタグを返す
                    return engine;
                }
            }
            catch { /* ignore */ }
            return null;
        }

        /// <summary>表示名（"日本語 (ja)" など）に整形。</summary>
        public static string ToDisplay(string tag)
        {
            try
            {
                var lang = new Language(tag);
                var native = lang.NativeName;  // 現地語表記
                if (string.IsNullOrEmpty(native)) native = lang.DisplayName; // 英語名
                return string.Format("{0} ({1})", native, lang.LanguageTag);
            }
            catch
            {
                return tag;
            }
        }

        private static string ToIso(string tagOrName)
        {
            if (string.IsNullOrEmpty(tagOrName)) return tagOrName;
            // "ja-JP" → "ja"
            var i = tagOrName.IndexOf('-');
            return (i > 0) ? tagOrName.Substring(0, i) : tagOrName;
        }
    }
}
