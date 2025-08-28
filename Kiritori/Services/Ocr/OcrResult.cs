using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Services.Ocr
{

    public sealed class OcrResult
    {
        public string Text { get; set; } = "";
        // オプション（null の可能性あり）
        public List<OcrWordBox> Words { get; set; } // may be null
    }
    public sealed class OcrWordBox
    {
        public string Text { get; set; } = "";
        public Rectangle Bounds { get; set; }
    }
}
