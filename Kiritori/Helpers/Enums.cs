using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Helpers
{
    public enum LoadMethod
    {
        Path,       // ファイルから（OpenFileDialog/ドラッグ&ドロップ/IPC）
        Capture,    // Kiritoriのキャプチャ結果（一時保存など）
        Clipboard,  // クリップボードから
        History     // 履歴から再オープン
    }
}
