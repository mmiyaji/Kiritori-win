using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
namespace Kiritori
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.WriteLine("デバッグ・メッセージを出力");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            new MainApplication();
            Application.Run();
        }
    }
}
