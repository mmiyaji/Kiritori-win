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
using Windows.Storage.Streams;

namespace Kiritori.Services.Ocr
{
    public sealed class WindowsOcrProvider : IOcrProvider
    {
        public string Name { get { return "Windows OCR"; } }
        public bool IsAvailable() { return true; }

        /// <summary>インストール済みの OCR 言語（BCP-47）一覧（例: "ja", "en-US"）</summary>
        public string[] GetSupportedLanguages()
        {
            try
            {
                return OcrEngine.AvailableRecognizerLanguages
                                .Select(l => l.LanguageTag)
                                .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public async Task<OcrResult> RecognizeAsync(Bitmap bmp, OcrOptions opt)
        {
            if (bmp == null) throw new ArgumentNullException(nameof(bmp));

            Bitmap working = null;
            SoftwareBitmap swb = null;
            try
            {
                // 1) 前処理（必要に応じて拡大）。拡大倍率を覚えておく（BoundingBox 補正用）
                float preprocessScale = 1.0f;
                if (opt != null && opt.Preprocess)
                {
                    working = Preprocess(bmp, out preprocessScale);
                }
                else
                {
                    working = (Bitmap)bmp.Clone();
                }

                // 2) SoftwareBitmap 化
                swb = await ToSoftwareBitmapAsync(working).ConfigureAwait(false);

                // 3) OCR エンジン作成
                //    希望言語（空/auto/未指定可）→ 最適タグ決定 → TryCreate
                string requestedTag = (opt != null ? opt.LanguageTag : null);
                string resolvedTag;
                var engine = TryCreateEngineWithFallback(requestedTag, out resolvedTag);
                if (engine == null)
                    throw new InvalidOperationException("Windows OCR engine is not available (no installed languages).");

                // 4) OCR 実行
                var native = await engine.RecognizeAsync(swb).AsTask().ConfigureAwait(false);

                // 5) テキスト整形（行単位で単語を結合 → 正規化）
                string text = "";
                if (native.Lines != null)
                {
                    var sb = new StringBuilder();
                    foreach (var line in native.Lines)
                    {
                        string lineText = string.Join(" ", line.Words.Select(w => w.Text));
                        lineText = NormalizeOcrText(lineText);
                        sb.AppendLine(lineText);
                    }
                    text = sb.ToString().TrimEnd();
                }

                // 6) バウンディングボックス（必要時）。前処理拡大を元倍率に補正
                List<OcrWordBox> words = null;
                if (opt != null && opt.ReturnWithBoundingBoxes && native.Lines != null)
                {
                    float invScale = (preprocessScale > 0f) ? (1.0f / preprocessScale) : 1.0f;
                    words = native.Lines
                        .SelectMany(l => l.Words)
                        .Select(w =>
                        {
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

                // 任意：ここで requestedTag と resolvedTag が異なる場合はログを出すと親切
                // if (!string.Equals(NormalizeTag(requestedTag), NormalizeTag(resolvedTag), StringComparison.OrdinalIgnoreCase))
                //     AppLog.Info($"[OCR] Language fallback: {requestedTag ?? "auto"} → {resolvedTag}");

                return new OcrResult { Text = text, Words = words };
            }
            finally
            {
                // 解放順：swb → working
                if (swb != null) swb.Dispose();
                if (working != null) working.Dispose();
            }
        }

        // ===== 内部ユーティリティ =====

        /// <summary>
        /// 希望タグ（null/""/"auto"可）から最適言語を解決し、OcrEngine を生成。
        /// 優先度: exact → 同一2文字言語 → CurrentUICulture → 英語 → 一覧先頭 → UserProfileLanguages
        /// </summary>
        private static OcrEngine TryCreateEngineWithFallback(string preferredOrAuto, out string resolvedTag)
        {
            resolvedTag = null;

            // まずはインストール済み一覧
            string[] available;
            try
            {
                available = OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToArray();
            }
            catch
            {
                available = Array.Empty<string>();
            }

            // 1) 明示指定があれば最適化（"auto" や空は無視）
            string picked = PickBestTag(preferredOrAuto, available);
            if (!string.IsNullOrEmpty(picked))
            {
                var eng = OcrEngine.TryCreateFromLanguage(new Language(picked));
                if (eng != null)
                {
                    resolvedTag = picked;
                    return eng;
                }
            }

            // 2) ユーザープロファイル言語
            var fromProfile = OcrEngine.TryCreateFromUserProfileLanguages();
            if (fromProfile != null)
            {
                // どのタグが選ばれたかは API から直接は取れないので、代表として UI 言語等を入れる
                // 必要なら AvailableRecognizerLanguages との交差で推定してもOK
                resolvedTag = GuessFromProfileOrUi(available);
                return fromProfile;
            }

            // 3) 一覧から最後の砦（先頭）
            if (available.Length > 0)
            {
                var eng = OcrEngine.TryCreateFromLanguage(new Language(available[0]));
                if (eng != null)
                {
                    resolvedTag = available[0];
                    return eng;
                }
            }

            // すべて失敗
            return null;
        }

        /// <summary>希望タグとインストール済みから最適タグを選ぶ。</summary>
        private static string PickBestTag(string preferredOrAuto, string[] available)
        {
            if (available == null || available.Length == 0) return null;

            var pref = (preferredOrAuto ?? "").Trim();
            if (string.Equals(pref, "auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(pref))
            {
                // auto の場合は UI 言語 → 英語 → 先頭
                var ui = ToIso(System.Globalization.CultureInfo.CurrentUICulture.Name);
                var fromUi = available.FirstOrDefault(t => string.Equals(ToIso(t), ui, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(fromUi)) return fromUi;

                var en = available.FirstOrDefault(t => string.Equals(ToIso(t), "en", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(en)) return en;

                return available[0];
            }

            // 1) 完全一致
            var exact = available.FirstOrDefault(t => string.Equals(t, pref, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exact)) return exact;

            // 2) 2文字コード一致
            var prefIso = ToIso(pref);
            var two = available.FirstOrDefault(t => string.Equals(ToIso(t), prefIso, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(two)) return two;

            // 3) UI 言語
            var uiIso = ToIso(System.Globalization.CultureInfo.CurrentUICulture.Name);
            var fromUi2 = available.FirstOrDefault(t => string.Equals(ToIso(t), uiIso, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(fromUi2)) return fromUi2;

            // 4) 英語
            var en2 = available.FirstOrDefault(t => string.Equals(ToIso(t), "en", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(en2)) return en2;

            // 5) 先頭
            return available[0];
        }

        private static string GuessFromProfileOrUi(string[] available)
        {
            if (available == null || available.Length == 0) return null;
            var uiIso = ToIso(System.Globalization.CultureInfo.CurrentUICulture.Name);
            var fromUi = available.FirstOrDefault(t => string.Equals(ToIso(t), uiIso, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(fromUi)) return fromUi;

            var en = available.FirstOrDefault(t => string.Equals(ToIso(t), "en", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(en)) return en;

            return available[0];
        }

        private static string ToIso(string tagOrName)
        {
            if (string.IsNullOrEmpty(tagOrName)) return tagOrName;
            var i = tagOrName.IndexOf('-');
            return (i > 0) ? tagOrName.Substring(0, i) : tagOrName;
        }

        // ===== 前処理・画像変換・整形 =====

        // 2倍拡大（必要に応じて調整可能）。倍率を out で返す
        private static Bitmap Preprocess(Bitmap src, out float scale)
        {
            scale = 2.0f;
            var outBmp = new Bitmap(
                (int)Math.Max(1, Math.Round(src.Width * scale)),
                (int)Math.Max(1, Math.Round(src.Height * scale)),
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
            // 中間 MemoryStream を使わず、InMemoryRandomAccessStream に直接 Save する
            using (var ras = new InMemoryRandomAccessStream())
            using (var s = ras.AsStreamForWrite())
            {
                // BMP で保存（互換性重視）: .NET の GDI+ → 直接WinRTラッパに書き込み
                bmp.Save(s, System.Drawing.Imaging.ImageFormat.Bmp);
                s.Flush();
                s.Position = 0; // 読み戻し位置を先頭に

                var dec = await BitmapDecoder.CreateAsync(ras);
                return await dec.GetSoftwareBitmapAsync(); // 呼び出し側で必要に応じて Dispose
            }
        }

        private static string NormalizeOcrText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 連続スペース → 単一スペース
            string t = System.Text.RegularExpressions.Regex.Replace(text, @"[ ]{2,}", " ");

            // 日本語の直前/直後の半角スペースを削る（ひらがな・カタカナ・CJK）
            t = System.Text.RegularExpressions.Regex.Replace(
                t, @"(?<=[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]) ", "");
            t = System.Text.RegularExpressions.Regex.Replace(
                t, @" (?=[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}])", "");

            return t.Trim();
        }
    }
}
