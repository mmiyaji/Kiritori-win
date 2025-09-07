using System;
using System.Drawing;
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

            // 言語の決定
            var lang = Properties.Settings.Default["OcrLanguage"] as string;
            if (string.IsNullOrWhiteSpace(lang))
            {
                var ui = Properties.Settings.Default.UICulture;
                lang = !string.IsNullOrEmpty(ui) ? ui : "ja";
            }

            var ocrService = new OcrService();
            var provider   = ocrService.Get(null);

            var opt = new OcrOptions
            {
                LanguageTag = lang,
                Preprocess  = preprocess,
                // CopyToClipboard は provider では使わないので参照しない
            };

            string text;
            using (var clone = new Bitmap(src)) // 元画像を汚さない
            {
                var result = await provider.RecognizeAsync(clone, opt).ConfigureAwait(false);
                text = result?.Text ?? string.Empty;
            }

            if (copyToClipboard && !string.IsNullOrEmpty(text))
                TrySetClipboardText(text);

            return text;
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
