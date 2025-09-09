using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kiritori.Services.Logging;

namespace Kiritori.Services.Extensions
{
    internal sealed class ExtDownloadDialog : Form
    {
        private readonly ExtensionManifest _manifest;
        private readonly System.Threading.CancellationTokenSource _cts = new System.Threading.CancellationTokenSource();

        private Label _lblTitle;
        private Label _lblStatus;
        private ProgressBar _bar;
        private Button _btnCancel;

        private ExtDownloadDialog(ExtensionManifest manifest)
        {
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            InitializeComponent();
        }

        public static bool Run(IWin32Window owner, ExtensionManifest manifest)
        {
            using (var dlg = new ExtDownloadDialog(manifest))
            {
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Shown += async (s, e) => await dlg.DoWorkAsync();
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "拡張機能のインストール";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ControlBox = false;
            this.ShowInTaskbar = false;
            this.ClientSize = new System.Drawing.Size(460, 140);

            _lblTitle = new Label
            {
                AutoSize = false,
                Text = $"{_manifest.DisplayName ?? _manifest.Id}  ({_manifest.Version})",
                Dock = DockStyle.Top,
                Height = 26
            };

            _lblStatus = new Label
            {
                AutoSize = false,
                Text = "準備中…",
                Dock = DockStyle.Top,
                Height = 24
            };

            _bar = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Dock = DockStyle.Top,
                Height = 28,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            _btnCancel = new Button
            {
                Text = "キャンセル",
                Dock = DockStyle.Bottom,
                Height = 32
            };
            _btnCancel.Click += (s, e) => _cts.Cancel();

            var pad = new Padding(16, 12, 16, 12);
            var panel = new Panel { Dock = DockStyle.Fill, Padding = pad };
            panel.Controls.Add(_btnCancel);
            panel.Controls.Add(_bar);
            panel.Controls.Add(_lblStatus);
            panel.Controls.Add(_lblTitle);

            this.Controls.Add(panel);
        }

        private Task DoWorkAsync()
        {
            return Task.Run(async () =>
            {
                try
                {
                    UpdateStatus($"ダウンロードを開始します…");
                    var sw = Stopwatch.StartNew();

                    var progress = new Progress<ExtensionsManager.DownloadProgress>(
                        p => {
                            if (p.Stage != null) UpdateStatus(p.Stage);
                            if (p.Percent >= 0) UpdatePercent(p.Percent);
                        });

                    var targetDir = await ExtensionsManager.InstallAsync(_manifest, progress, _cts.Token);

                    sw.Stop();
                    Log.Info($"[Ext] Install finished: {_manifest.Id} -> {targetDir} ({sw.ElapsedMilliseconds}ms)", "Extensions");

                    this.SafeInvoke(() => { this.DialogResult = DialogResult.OK; this.Close(); });
                }
                catch (OperationCanceledException)
                {
                    Log.Warn($"[Ext] Install canceled: {_manifest.Id}", "Extensions");
                    this.SafeInvoke(() => { this.DialogResult = DialogResult.Cancel; this.Close(); });
                }
                catch (Exception ex)
                {
                    Log.Error($"[Ext] Install failed: {_manifest.Id} ({ex.Message})", "Extensions");
                    this.SafeInvoke(() =>
                    {
                        MessageBox.Show(this, "インストールに失敗しました:\n" + ex.Message, "Kiritori Extensions",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.DialogResult = DialogResult.Abort;
                        this.Close();
                    });
                }
            });
        }

        private void UpdateStatus(string text)
        {
            this.SafeInvoke(() => _lblStatus.Text = text);
        }
        private void UpdatePercent(int percent)
        {
            if (percent < 0) percent = 0; if (percent > 100) percent = 100;
            this.SafeInvoke(() => _bar.Value = percent);
        }
    }

    internal static class ControlInvokeExtensions
    {
        public static void SafeInvoke(this Control c, Action a)
        {
            if (c.IsDisposed) return;
            if (c.InvokeRequired) c.BeginInvoke(a);
            else a();
        }
    }
}
