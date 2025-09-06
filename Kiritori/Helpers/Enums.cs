using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kiritori.Helpers
{
    public enum LoadMethod
    {
        Path,       // ファイルから（OpenFileDialog/ドラッグ&ドロップ/IPC）
        Capture,    // Kiritoriのキャプチャ結果（一時保存など）
        Clipboard,  // クリップボードから
        History     // 履歴から再オープン
    }
    internal enum HOTS
    {
        MOVE_LEFT = Keys.Left,
        MOVE_RIGHT = Keys.Right,
        MOVE_UP = Keys.Up,
        MOVE_DOWN = Keys.Down,
        SHIFT_MOVE_LEFT = Keys.Left | Keys.Shift,
        SHIFT_MOVE_RIGHT = Keys.Right | Keys.Shift,
        SHIFT_MOVE_UP = Keys.Up | Keys.Shift,
        SHIFT_MOVE_DOWN = Keys.Down | Keys.Shift,
        FLOAT = Keys.Control | Keys.A,
        SHADOW = Keys.Control | Keys.D,
        HOVER = Keys.Control | Keys.F,
        TITLEBAR = Keys.Control | Keys.U,
        SAVE = Keys.Control | Keys.S,
        LOAD = Keys.Control | Keys.O,
        OPEN = Keys.Control | Keys.N,
        EDIT_MSPAINT = Keys.Control | Keys.E,
        ZOOM_ORIGIN_NUMPAD = Keys.Control | Keys.NumPad0,
        ZOOM_ORIGIN_MAIN = Keys.Control | Keys.D0,
        ZOOM_IN = Keys.Control | Keys.Oemplus,
        ZOOM_OUT = Keys.Control | Keys.OemMinus,
        LOCATE_ORIGIN_MAIN = Keys.Control | Keys.D9,
        CLOSE = Keys.Control | Keys.W,
        ESCAPE = Keys.Escape,
        SPACE = Keys.Space,
        COPY = Keys.Control | Keys.C,
        CUT = Keys.Control | Keys.X,
        OCR = Keys.Control | Keys.T,
        PRINT = Keys.Control | Keys.P,
        MINIMIZE = Keys.Control | Keys.H,
        INFO = Keys.Control | Keys.I,
        SETTING = Keys.Control | Keys.Oemcomma,
        EXIT = Keys.Control | Keys.Q,
        RECORD = Keys.Control | Keys.R,
    }
    [Flags]
    public enum MOD_KEY : int
    {
        ALT = 0x0001,
        CONTROL = 0x0002,
        SHIFT = 0x0004,
        // 任意: NOREPEAT も使えます（チャタリング防止）
        // NOREPEAT = 0x4000,
        WIN = 0x0008, // 使う場合は HotkeyPicker 側も対応を
    }
    public enum ResizeAnchor
    {
        None,
        TopLeft, TopRight, BottomLeft, BottomRight,
        Left, Right, Top, Bottom
    }
    public enum CaptureMode
    {
        image,
        ocr,
        live,
    }
    public enum RenderPolicy
    {
        AlwaysDraw = 0,
        HashSkip = 1
    }
    public enum OutputKind {
        Mp4,
        Gif
    }

}
