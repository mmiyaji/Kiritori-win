using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Kiritori.Services.Ocr
{
    public sealed class WindowsOcrProvider : IOcrProvider
    {
        public string Name { get { return "Windows OCR"; } }
        public bool IsAvailable() { return true; }

        public string[] GetSupportedLanguages()
        {
            return OcrEngine.AvailableRecognizerLanguages
                            .Select(l => l.LanguageTag).ToArray();
        }

        public async Task<OcrResult> RecognizeAsync(Bitmap bmp, OcrOptions opt)
        {
            Bitmap working = null;
            SoftwareBitmap swb = null;
            try
            {
                // 1) 前処理（拡大など）。拡大倍率を覚えておく（BoundingBox 補正用）
                float preprocessScale = 1.0f;
                if (opt != null && opt.Preprocess)
                {
                    working = Preprocess(bmp, out preprocessScale); // ← out で倍率を返す版にしておく
                }
                else
                {
                    working = (Bitmap)bmp.Clone();
                }

                // 2) SoftwareBitmap 化（必ず解放）
                swb = await ToSoftwareBitmapAsync(working).ConfigureAwait(false);

                // 3) OCR エンジン作成（OSに言語未導入ならユーザープロファイル言語にフォールバック）
                var langTag = (opt != null && !string.IsNullOrEmpty(opt.LanguageTag)) ? opt.LanguageTag : "ja";
                var lang = new Windows.Globalization.Language(langTag);
                var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(lang) 
                            ?? Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();

                if (engine == null)
                    throw new InvalidOperationException("OCR engine is not available on this system.");

                // 4) OCR 実行
                var native = await engine.RecognizeAsync(swb).AsTask().ConfigureAwait(false);

                // 5) テキスト取り出し＋正規化（日本語の前後スペース除去・連続スペース縮約）
                string text = native.Text != null ? native.Text.Replace("\r\n", "\n") : "";
                text = NormalizeOcrText(text);

                // 6) バウンディングボックス（必要時）。前処理で拡大したなら元倍率へ補正
                System.Collections.Generic.List<OcrWordBox> words = null;
                if (opt != null && opt.ReturnWithBoundingBoxes && native.Lines != null)
                {
                    float invScale = (preprocessScale > 0f) ? (1.0f / preprocessScale) : 1.0f;
                    words = native.Lines
                        .SelectMany(l => l.Words)
                        .Select(w =>
                        {
                            // OCRは前処理後の座標なので、原画像系へ戻す
                            int x = (int)Math.Round(w.BoundingRect.X * invScale);
                            int y = (int)Math.Round(w.BoundingRect.Y * invScale);
                            int wdt = (int)Math.Round(w.BoundingRect.Width * invScale);
                            int hgt = (int)Math.Round(w.BoundingRect.Height * invScale);
                            if (wdt < 1) wdt = 1;
                            if (hgt < 1) hgt = 1;

                            return new OcrWordBox
                            {
                                Text = w.Text,
                                Bounds = new Rectangle(x, y, wdt, hgt)
                            };
                        })
                        .ToList();
                }

                return new OcrResult { Text = text, Words = words };
            }
            finally
            {
                // 解放順：swb → working
                if (swb != null) swb.Dispose();
                if (working != null) working.Dispose();
            }
        }
        // 2倍拡大してコントラスト少し上げる等。倍率を out で返す
        private static Bitmap Preprocess(Bitmap src, out float scale)
        {
            scale = 2.0f; // 例：2x
            var outBmp = new Bitmap(
                (int)(src.Width * scale),
                (int)(src.Height * scale),
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            using (var g = Graphics.FromImage(outBmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, new Rectangle(0, 0, outBmp.Width, outBmp.Height));
            }
            // 必要ならガンマ/コントラスト調整を追加
            return outBmp;
        }

        private static Bitmap Preprocess(Bitmap src)
        {
            float scale = 2.0f;
            var outBmp = new Bitmap(
                (int)(src.Width * scale),
                (int)(src.Height * scale),
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            using (var g = Graphics.FromImage(outBmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, new Rectangle(0, 0, outBmp.Width, outBmp.Height));
            }
            return outBmp;
        }

        private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                ms.Position = 0;

                var ras = ms.AsRandomAccessStream();
                var dec = await BitmapDecoder.CreateAsync(ras);
                return await dec.GetSoftwareBitmapAsync();
            }
        }
        private static string NormalizeOcrText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 連続スペース → 単一スペース
            string t = System.Text.RegularExpressions.Regex.Replace(text, @"[ ]{2,}", " ");

            // 日本語の直前/直後に入った半角スペースを削除
            // 和字の定義：ひらがな・カタカナ・CJK統合漢字
            t = System.Text.RegularExpressions.Regex.Replace(
                t, @"(?<=[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]) ", "");

            t = System.Text.RegularExpressions.Regex.Replace(
                t, @" (?=[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}])", "");

            return t.Trim();
        }

    }
}
