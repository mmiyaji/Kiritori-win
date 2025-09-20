using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kiritori.Services.Ocr;

namespace Kiritori.Services.Ocr
{
    public static class OcrFacade
    {
        /// <summary>
        /// 任意の Bitmap をOCRして結果文字列を返す。
        /// Settings から言語を取得し、必要ならクリップボードへもコピー。
        /// </summary>
        public static async Task<string> RunAsync(Bitmap src, bool copyToClipboard = false, bool preprocess = true)
        {
            if (src == null) return string.Empty;

            var setting = Properties.Settings.Default["OcrLanguage"] as string; // "auto" / "ja" / "en" / など
            var ocrService = new OcrService();
            var provider   = ocrService.Get(null);

            // ★ auto時は ja→en 優先で実際に使えるタグを決定
            var lang = ResolveOcrLanguage(provider, setting);

            var opt = new OcrOptions {
                LanguageTag = lang,
                Preprocess  = preprocess,
            };

            string text;
            using (var clone = new Bitmap(src))
            {
                var result = await provider.RecognizeAsync(clone, opt).ConfigureAwait(false);
                text = result?.Text ?? string.Empty;
            }

            if (copyToClipboard && !string.IsNullOrEmpty(text))
                TrySetClipboardText(text);

            return text;
        }
        private static string CanonicalLang(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "";
            var t = tag.ToLowerInvariant();
            if (t == "jpn") return "ja";   // Tesseract
            if (t == "eng") return "en";   // Tesseract
            var i = t.IndexOf('-');        // BCP-47 → ベース言語
            return (i > 0) ? t.Substring(0, i) : t;
        }

        private static string[] SafeSupported(IOcrProvider provider)
        {
            try {
                var a = provider?.GetSupportedLanguages();
                return (a != null && a.Length > 0) ? a : new string[0];
            } catch { return new string[0]; }
        }

        private static string ResolveOcrLanguage(IOcrProvider provider, string setting)
        {
            // auto 以外はそのまま（必要ならここで検証して置換してもOK）
            if (!string.IsNullOrWhiteSpace(setting) &&
                !setting.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return setting;

            var supported = SafeSupported(provider);                 // 例: ["ja-JP","en-US"] or ["jpn","eng"]
            var ja = supported.FirstOrDefault(s => CanonicalLang(s) == "ja");
            if (!string.IsNullOrEmpty(ja)) return ja;

            var en = supported.FirstOrDefault(s => CanonicalLang(s) == "en");
            if (!string.IsNullOrEmpty(en)) return en;

            if (supported.Length > 0) return supported[0];          // 何かは使える
            return "en";                                            // 最終フォールバック
        }
        private static void TrySetClipboardText(string text)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch
            {
                // 呼び出し元がMTAでも安全にコピーできるようにSTAスレッドでフォールバック
                var done = new ManualResetEventSlim(false);
                var th = new Thread(() =>
                {
                    try { Clipboard.SetText(text); } catch { /* ignore */ }
                    finally { done.Set(); }
                });
                th.SetApartmentState(ApartmentState.STA);
                th.IsBackground = true;
                th.Start();
                done.Wait(500); // 最大0.5秒だけ待つ
            }
        }
    }
}
