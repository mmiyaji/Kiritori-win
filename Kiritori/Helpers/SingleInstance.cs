using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Kiritori.Helpers
{
    internal static class SingleInstance
    {
        private const int MaxIpcPathCount = 64;
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
            var sendPaths = NormalizeImagePaths(paths);
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None))
                    {
                        client.Connect(singleAttemptMs); // ここで待つ
                        using (var bw = new BinaryWriter(client, Encoding.UTF8))
                        {
                            bw.Write(sendPaths.Length);
                            foreach (var p in sendPaths) bw.Write(p ?? string.Empty);
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
                                if (n < 0 || n > MaxIpcPathCount) throw new InvalidDataException("Invalid IPC file count.");

                                var raw = new string[n];
                                for (int i = 0; i < raw.Length; i++) raw[i] = br.ReadString();
                                var list = NormalizeImagePaths(raw);
                                if (list.Length == 0) continue;

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
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None))
                    client.Connect(100);
            }
            catch { }
            try { _serverThread?.Join(500); } catch { }
        }

        private static string[] NormalizeImagePaths(IEnumerable<string> paths)
        {
            if (paths == null) return Array.Empty<string>();
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in paths)
            {
                if (result.Count >= MaxIpcPathCount) break;
                if (string.IsNullOrWhiteSpace(raw)) continue;

                try
                {
                    var path = raw.Trim('"');
                    if (!File.Exists(path)) continue;
                    if (!ImageFileSupport.IsSupportedImagePath(path)) continue;

                    path = Path.GetFullPath(path);
                    if (seen.Add(path)) result.Add(path);
                }
                catch { }
            }

            return result.ToArray();
        }

    }
}
