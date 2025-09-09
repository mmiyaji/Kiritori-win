using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kiritori.Services.Logging;
using Kiritori.Helpers;

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
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            // --- 事前確認ダイアログ（サイズ表示つき） ---
            var sizeBytes = manifest.Download?.Size ?? 0L;
            var sizeText = sizeBytes > 0
                ? FormatBytes(sizeBytes)
                : SR.T("Extensions.InstallDialog.SizeUnknown", "Unknown");

            var name = manifest.DisplayName ?? manifest.Id ?? "(unknown)";
            var ver = manifest.Version ?? "-";
            var url = manifest.Download?.Url ?? "-";
            var sha = manifest.Download?.Sha256;
            var shaShort = (!string.IsNullOrEmpty(sha) && sha.Length >= 8) ? sha.Substring(0, 8) : "-";

            var title = SR.T("Extensions.InstallDialog.ConfirmTitle", "Install Extension");
            var body = string.Format(
                "Install \"{0}\" ({1})?\r\nDownload size: {2}\r\nSource: {3}\r\nSHA-256: {4}",
                name, ver, sizeText, url, shaShort);

            var btn = MessageBox.Show(owner, body, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (btn != DialogResult.OK) return false;

            // --- 既存のダイアログを開いてダウンロード実行 ---
            using (var dlg = new ExtDownloadDialog(manifest))
            {
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Shown += async (s, e) => await dlg.DoWorkAsync();
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }

        private void InitializeComponent()
        {
            this.Text = SR.T("Extensions.InstallDialog.Title", "Install Extension");
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
                Text = SR.T("Extensions.InstallDialog.Preparing", "Preparing..."),
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
                Text = SR.T("Extensions.InstallDialog.Cancel", "Cancel"),
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
                    UpdateStatus(SR.T("Extensions.InstallDialog.StartingDownload", "Starting download..."));
                    var sw = Stopwatch.StartNew();

                    var progress = new Progress<ExtensionsManager.DownloadProgress>(
                        p =>
                        {
                            if (p.Stage != null) UpdateStatus(p.Stage);
                            if (p.Percent >= 0) UpdatePercent(p.Percent);
                        });

                    var targetDir = await ExtensionsManager.InstallAsync(_manifest, progress, _cts.Token);

                    sw.Stop();
                    Log.Info($"Install finished: {_manifest.Id} -> {targetDir} ({sw.ElapsedMilliseconds}ms)", "Extensions");

                    this.SafeInvoke(() => { this.DialogResult = DialogResult.OK; this.Close(); });
                }
                catch (OperationCanceledException)
                {
                    Log.Warn($"Install canceled: {_manifest.Id}", "Extensions");
                    this.SafeInvoke(() => { this.DialogResult = DialogResult.Cancel; this.Close(); });
                }
                catch (Exception ex)
                {
                    Log.Error($"Install failed: {_manifest.Id} ({ex.Message})", "Extensions");
                    this.SafeInvoke(() =>
                    {
                        MessageBox.Show(this, SR.T("Extensions.InstallDialog.InstallFailed", "Installation failed:") + "\n" + ex.Message, "Kiritori Extensions",
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
        private static string FormatBytes(long bytes)
        {
            const long K = 1024, M = K * 1024, G = M * 1024;
            if (bytes >= G) return (bytes / (double)G).ToString("0.0") + " GiB";
            if (bytes >= M) return (bytes / (double)M).ToString("0.0") + " MiB";
            if (bytes >= K) return (bytes / (double)K).ToString("0.0") + " KiB";
            if (bytes > 0)  return bytes + " B";
            return SR.T("Extensions.InstallDialog.SizeUnknown", "Unknown");
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
