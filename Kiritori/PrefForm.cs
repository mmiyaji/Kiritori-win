using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
//using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kiritori
{
    public partial class PrefForm : Form
    {
        public PrefForm()
        {
            InitializeComponent();
        }

        private void PrefForm_Load(object sender, EventArgs e)
        {
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupDir, Application.ProductName + ".lnk");
            checkBox3.Checked = File.Exists(shortcutPath);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
            PrefForm.ActiveForm.Close();
            SetStartupShortcut(Properties.Settings.Default.isStartup);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PrefForm.ActiveForm.Close();
        
        }

        /// <summary>
        /// </summary>
        private static void SetStartupShortcut(bool enable)
        {
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupDir, Application.ProductName + ".lnk");

            if (!enable)
            {
                if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
                return;
            }

            string targetPath = Application.ExecutablePath;

            // WshShell COM を手動で呼び出す
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
            object shell = Activator.CreateInstance(t);

            object shortcut = t.InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell,
                new object[] { shortcutPath });

            try
            {
                t.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty,
                    null, shortcut, new object[] { targetPath });
                t.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty,
                    null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
                t.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty,
                    null, shortcut, new object[] { targetPath + ",0" });

                t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod,
                    null, shortcut, null);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            // チェックONならスタートアップ登録、OFFなら削除
            SetStartupShortcut(checkBox3.Checked);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //リンク先に移動したことにする
            linkLabel1.LinkVisited = true;
            //ブラウザで開く
            System.Diagnostics.Process.Start("http://kiritori.ruhenheim.org");
        }
    }
}
