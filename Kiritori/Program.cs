using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Form1 f1 = new Form1();
            f1.Show();
            f1.SetDesktopLocation(1000, 20);

            Form1 f2 = new Form1();
            f2.Show();
            Form1 f3 = new Form1();
            f3.Show();
            Application.Run(new Form2());
        }
    }
}
