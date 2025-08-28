using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Services.Ocr
{
    public interface IOcrProvider
    {
        string Name { get; }
        bool IsAvailable();                       // 実行可否（依存の有無など）
        string[] GetSupportedLanguages();         // 推定可（Windows OCRはOS依存）
        Task<OcrResult> RecognizeAsync(Bitmap bmp, OcrOptions opt);
    }
}
