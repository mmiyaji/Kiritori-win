using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
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

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
            PrefForm.ActiveForm.Close();
            if (Properties.Settings.Default.isStartup)
            {
                SetCurrentVersionRun();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PrefForm.ActiveForm.Close();
        
        }

        /// <summary>
        /// CurrentUserのRunにアプリケーションの実行ファイルパスを登録する
        /// http://dobon.net/vb/dotnet/file/createshortcut.html
        /// </summary>
        private static void SetCurrentVersionRun()
        {
            //作成するショートカットのパス
            string shortcutPath = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Startup),
                Application.ProductName + @".lnk");
            if (!System.IO.File.Exists(shortcutPath))
            {
                //ショートカットのリンク先
                string targetPath = Application.ExecutablePath;

                //WshShellを作成
                Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
                object shell = Activator.CreateInstance(t);

                //WshShortcutを作成
                object shortcut = t.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell,
                    new object[] { shortcutPath });
                try {
                    //リンク先
                    t.InvokeMember("TargetPath",
                        System.Reflection.BindingFlags.SetProperty, null, shortcut,
                        new object[] { targetPath });
                    //アイコンのパス
                    t.InvokeMember("IconLocation",
                        System.Reflection.BindingFlags.SetProperty, null, shortcut,
                        new object[] { Application.ExecutablePath + ",0" });
                    //その他のプロパティも同様に設定できるため、省略

                    //ショートカットを作成
                    t.InvokeMember("Save",
                        System.Reflection.BindingFlags.InvokeMethod,
                        null, shortcut, null);
                }finally{
                    //後始末
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                }
            }
        }
        /// <summary>
        /// スタートアップにショートカットファイルあり かつ　設定解除した際に警告を表示→システムでは自動削除しない方針
        /// </summary>
        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            //作成するショートカットのパス
            string shortcutPath = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Startup),
                Application.ProductName + @".lnk");
            if (!checkBox3.Checked && System.IO.File.Exists(shortcutPath))
            {
                MessageBox.Show("Please delete shortcut file(" + shortcutPath + ") on your own.",
                    "Warnning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);                
            }
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
