using System;
using System.Drawing;
using System.Threading.Tasks;
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

            // 言語は Settings から。未設定なら UI のカルチャ or "ja" にフォールバック。
            var lang = Properties.Settings.Default["OcrLanguage"] as string;
            if (string.IsNullOrWhiteSpace(lang))
                lang = Properties.Settings.Default.UICulture ?? "ja";   // 既存のUICultureはPrefで管理されています :contentReference[oaicite:0]{index=0}

            var ocrService = new OcrService();
            var provider = ocrService.Get(null);

            var opt = new OcrOptions
            {
                LanguageTag = lang,
                Preprocess = preprocess,
                CopyToClipboard = copyToClipboard,
            };

            using (var clone = new Bitmap(src)) // 元画像を汚さない
            {
                var result = await provider.RecognizeAsync(clone, opt).ConfigureAwait(false);
                return result?.Text ?? string.Empty;
            }
        }

        /// <summary>Imageからのオーバーロード（必要なら）</summary>
        public static Task<string> RunAsync(Image image, bool copyToClipboard = false, bool preprocess = true)
            => RunAsync(image as Bitmap ?? new Bitmap(image), copyToClipboard, preprocess);
    }
}
