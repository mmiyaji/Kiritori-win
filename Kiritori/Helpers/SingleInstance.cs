using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

namespace Kiritori.Helpers
{
    internal static class SingleInstance
    {
        // SingleInstance.cs
        private static readonly string UserName = WindowsIdentity.GetCurrent()?.User?.Value ?? Environment.UserName;
        public static readonly string MutexName = $@"Local\Kiritori.SingleInstance.{UserName}";
        private static readonly string PipeName = $@"Kiritori.SingleInstance.{UserName}";

        private static Thread _serverThread;
        private static volatile bool _running;
        private static Action<string[]> _onFiles;
        private static readonly ConcurrentQueue<string[]> _pending = new ConcurrentQueue<string[]>();


        /// <summary>
        /// 既存インスタンスへファイルパス配列を送信できたら true（＝このプロセスは終了して良い）
        /// </summary>
        public static bool TrySendToExisting(string[] paths, int singleAttemptMs = 500, int maxAttempts = 8)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None))
                    {
                        client.Connect(singleAttemptMs); // ここで待つ
                        using (var bw = new BinaryWriter(client, Encoding.UTF8))
                        {
                            bw.Write(paths?.Length ?? 0);
                            if (paths != null) foreach (var p in paths) bw.Write(p ?? string.Empty);
                            bw.Flush();
                        }
                        return true; // 送れた
                    }
                }
                catch
                {
                    // 次の試行まで少し待つ（指数バックオフ）
                    Thread.Sleep(100 + i * 120);
                }
            }
            return false; // 最後まで接続できず
        }

        /// <summary>
        /// 受信サーバを開始。受信時に onFiles を呼ぶ（UIスレッドにマーシャリングは呼び出し側で）
        /// </summary>
        public static void StartServer(Action<string[]> onFiles)
        {
            _onFiles = onFiles;
            _running = true;
            _serverThread = new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                        {
                            server.WaitForConnection();

                            using (var br = new BinaryReader(server, Encoding.UTF8))
                            {
                                int n = br.ReadInt32();
                                var list = new string[Math.Max(0, n)];
                                for (int i = 0; i < list.Length; i++) list[i] = br.ReadString();

                                var h = _onFiles;
                                if (h != null) h(list);
                                else _pending.Enqueue(list); // まだハンドラ未設定ならキューへ
                            }
                        }
                    }
                    catch { /* keep looping */ }
                }
            });
            _serverThread.IsBackground = true;
            _serverThread.SetApartmentState(ApartmentState.MTA);
            _serverThread.Start();
        }

        // MainApplication が用意できたら呼ぶ
        public static void SetHandler(Action<string[]> onFiles)
        {
            _onFiles = onFiles;
            while (_pending.TryDequeue(out var x))
                _onFiles?.Invoke(x);
        }

        public static void StopServer()
        {
            _running = false;
            try { _serverThread?.Join(200); } catch { }
        }


    }
}
