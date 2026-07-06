using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritori.Views.LiveCapture
{
    internal sealed class LiveCaptureFrameEventArgs : EventArgs
    {
        public LiveCaptureFrameEventArgs(Bitmap bitmap, bool transfersOwnership)
        {
            Bitmap = bitmap;
            TransfersOwnership = transfersOwnership;
        }

        public Bitmap Bitmap { get; }
        public bool TransfersOwnership { get; }
    }

    internal interface LiveCaptureBackend : IDisposable
    {
        // 画面上のキャプチャ矩形（スクリーン座標）
        Rectangle CaptureRect { get; set; }

        // 新フレームが届いたら通知。所有権はフレーム単位のイベント引数で通知する。
        event Action<LiveCaptureFrameEventArgs> FrameArrived;

        // 開始／停止
        void Start();
        void Stop();

        // フレームレート上限（例: 30）
        int MaxFps { get; set; }
    }
}
