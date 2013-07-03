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
//            SnapWindow f1 = new SnapWindow();
//            f1.Show();
//            SnapWindow f2 = new SnapWindow();
//            f2.Show();
            Application.Run(new Form2());
        }
    }
}
