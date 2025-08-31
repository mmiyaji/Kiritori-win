using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Views.LiveCapture
{
    internal interface LiveCaptureBackend : IDisposable
    {
        // 画面上のキャプチャ矩形（スクリーン座標）
        Rectangle CaptureRect { get; set; }

        // 新フレームが届いたら通知（使い回しBitmapは呼び出し側でコピー不要）
        event Action<Bitmap> FrameArrived;

        // 開始／停止
        void Start();
        void Stop();

        // フレームレート上限（例: 30）
        int MaxFps { get; set; }
    }
}
