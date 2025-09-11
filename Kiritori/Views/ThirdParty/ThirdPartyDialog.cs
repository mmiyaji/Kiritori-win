using Kiritori.Services.ThirdParty;
using Kiritori.Services.Logging;
using Kiritori.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Kiritori.Views
{
    internal sealed class ThirdPartyDialog : Form
    {
        private SplitContainer _split;
        private TextBox _txtSearch;
        private ComboBox _cmbLicenseFilter;
        private ListView _lv;
        private RichTextBox _rtb;
        private Button _btnOpenProject;
        private Button _btnOpenLicenseUrl;
        private Button _btnCopy;
        private Button _btnSave;
        private CheckBox _chkWrap;
        private List<ThirdPartyComponent> _all = new List<ThirdPartyComponent>();
        private List<ThirdPartyComponent> _view = new List<ThirdPartyComponent>();

        public ThirdPartyDialog()
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.Text = "Third-Party Licenses";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(1000, 680);
            this.Icon = Properties.Resources.AppIcon;
            this.MinimizeBox = false;
            this.MaximizeBox = true;

            BuildUi();

            // ← ここで SplitContainer の初期幅を設定（右ペインを広めに）
            // ClientSize が確定してから安全に設定
            this.Load += (s, e) =>
            {
                // 左ペイン: 40% / 右ペイン: 60% くらい
                _split.Panel1MinSize = 220;
                _split.Panel2MinSize = 360;
                _split.SplitterWidth = 7;
                _split.SplitterDistance = Math.Max(_split.Panel1MinSize,
                    (int)(this.ClientSize.Width * 0.40));
            };

            try
            {
                _all = ThirdPartyCatalog.LoadFromEmbedded();
                if (_all == null || _all.Count == 0)
                {
                    _rtb.Text = "thirdparty.json (embedded resource) not found or empty.";
                }
                RebuildLicenseFilter();
                ApplyFilter(false); // 初期表示時は通常
            }
            catch
            {
                _rtb.Text = "Failed to load embedded third-party catalog.";
            }
        }

        private void BuildUi()
        {
            _split = new SplitContainer();
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            // SplitterDistance は Load で設定
            this.Controls.Add(_split);

            // 左
            var leftPanel = new TableLayoutPanel();
            leftPanel.Dock = DockStyle.Fill;
            leftPanel.ColumnCount = 1;
            leftPanel.RowCount = 3;
            leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _txtSearch = new TextBox();
            _txtSearch.Margin = new Padding(8, 8, 8, 4);
            _txtSearch.Width = 200;
            SetPlaceholder(_txtSearch, "Search (name, license, text)…");
            _txtSearch.TextChanged += (s, e) => { ApplyFilter(true); }; // ← 検索入力時はフォーカス保持

            _cmbLicenseFilter = new ComboBox();
            _cmbLicenseFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbLicenseFilter.Margin = new Padding(8, 4, 8, 4);
            _cmbLicenseFilter.SelectedIndexChanged += (s, e) => { ApplyFilter(false); };

            _lv = new ListView();
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.HideSelection = false;
            _lv.MultiSelect = false;
            _lv.Dock = DockStyle.Fill;
            _lv.Margin = new Padding(8, 4, 8, 8);
            _lv.Columns.Add("Name");
            _lv.Columns.Add("Version");
            _lv.Columns.Add("License");
            _lv.SelectedIndexChanged += (s, e) => { ShowSelected(); };

            leftPanel.Controls.Add(_txtSearch, 0, 0);
            leftPanel.Controls.Add(_cmbLicenseFilter, 0, 1);
            leftPanel.Controls.Add(_lv, 0, 2);
            _split.Panel1.Controls.Add(leftPanel);

            // 右
            var rightPanel = new TableLayoutPanel();
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.ColumnCount = 1;
            rightPanel.RowCount = 2;
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightPanel.Padding = new Padding(8);      // ← 余白
            _split.Panel2.Controls.Add(rightPanel);

            var btnRow = new FlowLayoutPanel();
            btnRow.AutoSize = true;
            btnRow.FlowDirection = FlowDirection.LeftToRight;
            btnRow.Dock = DockStyle.Top;

            _btnOpenProject = new Button { Text = "Open Project", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            _btnOpenProject.Click += (s, e) => { OpenSelectedUrl("project"); };
            _btnOpenLicenseUrl = new Button { Text = "Open License", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            _btnOpenLicenseUrl.Click += (s, e) => { OpenSelectedUrl("license"); };
            _btnCopy = new Button { Text = "Copy Text", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            _btnCopy.Click += (s, e) => { try { if (!string.IsNullOrEmpty(_rtb.Text)) Clipboard.SetText(_rtb.Text); } catch { } };
            _btnSave = new Button { Text = "Save As…", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            _btnSave.Click += (s, e) => { SaveCurrentText(); };
            _chkWrap = new CheckBox { Text = "Wrap", Checked = true, AutoSize = true, Margin = new Padding(16, 6, 0, 0) };
            _chkWrap.CheckedChanged += (s, e) => { _rtb.WordWrap = _chkWrap.Checked; };

            btnRow.Controls.AddRange(new Control[] { _btnOpenProject, _btnOpenLicenseUrl, _btnCopy, _btnSave, _chkWrap });

            _rtb = new RichTextBox();
            _rtb.ReadOnly = true;
            _rtb.Dock = DockStyle.Fill;
            _rtb.BorderStyle = BorderStyle.FixedSingle;
            _rtb.Font = new Font("Consolas", 10f);
            _rtb.WordWrap = true;

            rightPanel.Controls.Add(btnRow, 0, 0);
            rightPanel.Controls.Add(_rtb, 0, 1);

            // _lv.Resize += (s, e) =>
            // {
            //     int w = _lv.ClientSize.Width;
            //     if (_lv.Columns.Count == 3 && w > 0)
            //     {
            //         _lv.Columns[0].Width = Math.Max(160, (int)(w * 0.58));
            //         _lv.Columns[1].Width = 90;
            //         _lv.Columns[2].Width = Math.Max(120, w - _lv.Columns[0].Width - _lv.Columns[1].Width - 4);
            //     }
            // };
        }


        private void RebuildLicenseFilter()
        {
            var kinds = new List<string>();
            kinds.Add("(All)");
            foreach (var g in _all.Select(a => a.License ?? "").Distinct().OrderBy(s => s))
            {
                if (!string.IsNullOrEmpty(g)) kinds.Add(g);
            }
            _cmbLicenseFilter.Items.Clear();
            foreach (var k in kinds) _cmbLicenseFilter.Items.Add(k);
            if (_cmbLicenseFilter.Items.Count > 0) _cmbLicenseFilter.SelectedIndex = 0;
        }

        private void ApplyFilter(bool preserveFocus)
        {
            string searchText;
            if (_txtSearch.ForeColor == Color.Gray)
                searchText = "";
            else
                searchText = _txtSearch.Text ?? "";

            string q = searchText.Trim().ToLowerInvariant();
            string lic = _cmbLicenseFilter.SelectedItem as string;
            bool allLic = string.IsNullOrEmpty(lic) || lic == "(All)";

            _view = new List<ThirdPartyComponent>();
            foreach (var c in _all)
            {
                if (!allLic && !string.Equals(c.License ?? "", lic ?? "", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (q.Length > 0)
                {
                    var sb = new StringBuilder();
                    sb.Append(c.Name).Append('\n')
                    .Append(c.Version).Append('\n')
                    .Append(c.License).Append('\n')
                    .Append(c.ProjectUrl).Append('\n')
                    .Append(c.LicenseUrl).Append('\n')
                    .Append(c.LicenseText).Append('\n')
                    .Append(c.NoticeText);

                    if (sb.ToString().ToLowerInvariant().IndexOf(q) < 0)
                        continue;
                }
                _view.Add(c);
            }

            _lv.BeginUpdate();
            _lv.Items.Clear();
            foreach (var c in _view)
            {
                var it = new ListViewItem(new string[] { c.Name ?? "", c.Version ?? "", c.License ?? "" });
                it.Tag = c;
                _lv.Items.Add(it);
            }
            _lv.EndUpdate();

            if (_lv.Items.Count > 0)
            {
                _lv.Items[0].Selected = true;
            }
            else
            {
                _rtb.Clear();
            }

            if (preserveFocus && this.ActiveControl == _txtSearch)
            {
                _txtSearch.Focus();
                _txtSearch.SelectionStart = _txtSearch.TextLength;
            }
            _lv.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            _lv.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }



        private void ShowSelected()
        {
            if (_lv.SelectedItems.Count == 0) { _rtb.Clear(); return; }
            var c = _lv.SelectedItems[0].Tag as ThirdPartyComponent;
            if (c == null) { _rtb.Clear(); return; }

            var sb = new StringBuilder();
            sb.AppendLine(c.Name + (string.IsNullOrEmpty(c.Version) ? "" : "  v" + c.Version));
            if (!string.IsNullOrEmpty(c.License)) sb.AppendLine("License: " + c.License);
            if (!string.IsNullOrEmpty(c.ProjectUrl)) sb.AppendLine("Project: " + c.ProjectUrl);
            if (!string.IsNullOrEmpty(c.LicenseUrl)) sb.AppendLine("License URL: " + c.LicenseUrl);
            sb.AppendLine();

            if (!string.IsNullOrEmpty(c.NoticeText))
            {
                sb.AppendLine("==== NOTICE ====");
                sb.AppendLine(c.NoticeText.Trim());
                sb.AppendLine();
            }
            if (!string.IsNullOrEmpty(c.LicenseText))
            {
                sb.AppendLine("==== LICENSE ====");
                sb.AppendLine(c.LicenseText.Trim());
            }

            _rtb.Clear();
            _rtb.Text = sb.ToString();
            _rtb.SelectionStart = 0;
            _rtb.ScrollToCaret();
        }


        private void OpenSelectedUrl(string kind)
        {
            if (_lv.SelectedItems.Count == 0) return;
            var c = _lv.SelectedItems[0].Tag as ThirdPartyComponent;
            if (c == null) return;

            string url = null;
            if (kind == "project") url = c.ProjectUrl;
            else if (kind == "license") url = c.LicenseUrl;

            if (!string.IsNullOrEmpty(url))
            {
                try { Process.Start(url); } catch { }
            }
        }

        private void SaveCurrentText()
        {
            if (string.IsNullOrEmpty(_rtb.Text)) return;
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*";
                sfd.FileName = "ThirdParty-Licenses.txt";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try { System.IO.File.WriteAllText(sfd.FileName, _rtb.Text, new UTF8Encoding(false)); }
                    catch { }
                }
            }
        }
        private string _searchPlaceholder = "Search (name, license, text)…";
        private bool _settingPlaceholder;

        // .NET 4.8 用の簡易プレースホルダー
        private void SetPlaceholder(TextBox tb, string placeholder)
        {
            _searchPlaceholder = placeholder ?? _searchPlaceholder;

            void ApplyPlaceholder()
            {
                _settingPlaceholder = true;
                try
                {
                    tb.ForeColor = Color.Gray;
                    tb.Text = _searchPlaceholder;
                }
                finally { _settingPlaceholder = false; }
            }

            void ClearPlaceholder()
            {
                _settingPlaceholder = true;
                try
                {
                    tb.Clear();
                    tb.ForeColor = SystemColors.WindowText;
                }
                finally { _settingPlaceholder = false; }
            }

            // 初期表示
            ApplyPlaceholder();

            // フォーカス取得で消す
            tb.Enter += (s, e) =>
            {
                if (tb.ForeColor == Color.Gray && tb.Text == _searchPlaceholder)
                    ClearPlaceholder();
            };

            // フォーカス喪失で空なら復元
            tb.Leave += (s, e) =>
            {
                if (string.IsNullOrEmpty(tb.Text))
                    ApplyPlaceholder();
            };

            // ユーザーが最初の1文字を打ったら自動でプレースホルダー解除
            tb.KeyPress += (s, e) =>
            {
                if (tb.ForeColor == Color.Gray)
                {
                    ClearPlaceholder();
                    // 直前のキーを反映（Backspace等は無視）
                    if (!char.IsControl(e.KeyChar))
                    {
                        tb.AppendText(e.KeyChar.ToString());
                        e.Handled = true;
                    }
                }
            };

            // TextChanged でフィルタが無駄に走らないように（呼び側でチェック）
            tb.TextChanged += (s, e) =>
            {
                if (_settingPlaceholder) return; // プレースホルダーの内部更新は無視
            };
        }

    }
}
