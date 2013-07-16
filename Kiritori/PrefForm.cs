using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //リンク先に移動したことにする
            linkLabel1.LinkVisited = true;
            //ブラウザで開く
            System.Diagnostics.Process.Start("http://kiritori.ruhenheim.org");
        }
        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
            PrefForm.ActiveForm.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PrefForm.ActiveForm.Close();
        }
    }
}
