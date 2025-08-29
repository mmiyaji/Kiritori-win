using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Services.Ocr
{

    public sealed class OcrOptions
    {
        public string LanguageTag { get; set; } = "ja";
        public bool CopyToClipboard { get; set; } = true;

        // null = 画像全体、指定あり = 部分OCR（バッチ）
        public Rectangle[] Regions { get; set; } // may be null

        public bool Preprocess { get; set; } = true;
        public bool ReturnWithBoundingBoxes { get; set; } = false;
    }

}
