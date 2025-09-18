using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kiritori.Services.Logging;
using Kiritori.Services.Extensions;
using Kiritori.Helpers;
namespace Kiritori
{
    public partial class PrefForm
    {
        // ===== Extensions タブ用 =====
        private DataGridView _gridExt;
        private BindingList<ExtRow> _extRows;
        private Button _btnExtInstallUpdate, _btnExtEnableDisable, _btnExtUninstall, _btnExtOpenFolder, _btnExtRefresh;

        private sealed class ExtRow
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string RepoVersion { get; set; }
            public string InstalledVersion { get; set; }
            public bool Installed { get; set; }
            public bool Enabled { get; set; }
            public string Description { get; set; }
            public Kiritori.Services.Extensions.ExtensionManifest ManifestRef { get; set; }
        }

        private void BuildExtensionsTab()
        {
            // ルート
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8),
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // グリッド
            _gridExt = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                MultiSelect = false,
                ScrollBars = ScrollBars.Both,
            };
            _gridExt.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            _gridExt.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

            // 列
            _gridExt.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = nameof(ExtRow.Name),
                HeaderText = SR.T("Column.Extensions.Name", "Name"),
                Width = 180 });
            _gridExt.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = nameof(ExtRow.Id),
                HeaderText = SR.T("Column.Extensions.Id", "Id"),
                Width = 130 });
            _gridExt.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = nameof(ExtRow.RepoVersion),
                HeaderText = SR.T("Column.Extensions.Repo", "Repo"),
                Width = 70 });
            _gridExt.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = nameof(ExtRow.InstalledVersion),
                HeaderText = SR.T("Column.Extensions.Installed", "Installed"),
                Width = 80 });
            _gridExt.Columns.Add(new DataGridViewCheckBoxColumn {
                DataPropertyName = nameof(ExtRow.Installed),
                HeaderText = SR.T("Column.Extensions.Installed.Checkbox", "I"),
                Width = 30,
                ToolTipText = SR.T("Column.Extensions.Installed.Tooltip", "Installed") });
            _gridExt.Columns.Add(new DataGridViewCheckBoxColumn {
                DataPropertyName = nameof(ExtRow.Enabled),
                HeaderText = SR.T("Column.Extensions.Enabled.Checkbox", "E"),
                Width = 30,
                ToolTipText = SR.T("Column.Extensions.Enabled.Tooltip", "Enabled") });
            var colDesc = new DataGridViewTextBoxColumn {
                DataPropertyName = nameof(ExtRow.Description),
                HeaderText = SR.T("Column.Extensions.Description", "Description"),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 60 };
            _gridExt.Columns.Add(colDesc);

            // ボタンバー
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0)
            };

            _btnExtInstallUpdate = new Button {
                Text = "Install / Update",
                Tag = "loc:Button.Extensions.InstallUpdate",
                AutoSize = true };
            _btnExtEnableDisable = new Button {
                Text = "Enable / Disable",
                Tag = "loc:Button.Extensions.EnableDisable",
                AutoSize = true };
            _btnExtUninstall = new Button {
                Text = "Uninstall",
                Tag = "loc:Button.Extensions.Uninstall",
                AutoSize = true };
            _btnExtOpenFolder = new Button {
                Text = "Open Folder",
                Tag = "loc:Button.Extensions.OpenFolder",
                AutoSize = true };
            _btnExtRefresh = new Button {
                Text = "Refresh",
                Tag = "loc:Button.Extensions.Refresh",
                AutoSize = true };

            _btnExtInstallUpdate.Click += (_, __) => DoInstallOrUpdateSelected();
            _btnExtEnableDisable.Click += (_, __) => DoEnableDisableSelected();
            _btnExtUninstall.Click += (_, __) => DoUninstallSelected();
            _btnExtOpenFolder.Click += (_, __) => DoOpenFolderSelected();
            _btnExtRefresh.Click += (_, __) => ReloadExtensionsIntoGrid();

            bar.Controls.AddRange(new Control[] {
                _btnExtRefresh, _btnExtInstallUpdate, _btnExtEnableDisable, _btnExtUninstall, _btnExtOpenFolder
            });

            _gridExt.SelectionChanged += (_, __) => UpdateExtButtons();

            // 追加
            root.Controls.Add(_gridExt, 0, 0);
            root.Controls.Add(bar, 0, 1);

            this.tabExtensions.Controls.Clear();
            this.tabExtensions.Controls.Add(root);

            // 初期ロード
            ReloadExtensionsIntoGrid();
        }

        private void ReloadExtensionsIntoGrid()
        {
            try
            {
                var rows = new List<ExtRow>();

                // リポジトリの全マニフェストを取得
                var manifests = ExtensionsManager.LoadRepoManifests() ?? Enumerable.Empty<ExtensionManifest>();
                foreach (var m in manifests)
                {
                    var id = m.Id ?? "";
                    var name = m.DisplayName ?? id;
                    var ver = m.Version ?? "";
                    var desc = m.Description ?? "";

                    var installed = ExtensionsManager.IsInstalled(id);
                    var enabled = installed && ExtensionsManager.IsEnabled(id);
                    var instVer = installed ? (ExtensionsManager.InstalledVersion(id) ?? "") : "";

                    rows.Add(new ExtRow
                    {
                        Id = id,
                        Name = name,
                        RepoVersion = ver,
                        InstalledVersion = instVer,
                        Installed = installed,
                        Enabled = enabled,
                        Description = desc,
                        ManifestRef = m
                    });
                }

                _extRows = new BindingList<ExtRow>(rows.OrderBy(r => r.Name).ToList());
                _gridExt.DataSource = _extRows;
                UpdateExtButtons();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load extensions list: " + ex.Message, "Extensions");
                MessageBox.Show(this, "Failed to load extension repository.\n" + ex.Message,
                    "Extensions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private ExtRow GetSelectedRow()
            => (_gridExt?.CurrentRow?.DataBoundItem as ExtRow);

        private void UpdateExtButtons()
        {
            var r = GetSelectedRow();
            bool has = r != null;
            _btnExtRefresh.Enabled = true;
            _btnExtOpenFolder.Enabled = has && r.Installed;
            _btnExtUninstall.Enabled = has && r.Installed;

            if (!has)
            {
                _btnExtInstallUpdate.Enabled = false;
                _btnExtEnableDisable.Enabled = false;
                _btnExtInstallUpdate.Text = SR.T("Button.Extensions.InstallUpdate", "Install / Update");
                _btnExtEnableDisable.Text = SR.T("Button.Extensions.EnableDisable", "Enable / Disable");
                return;
            }

            // Install/Update
            bool needsInstall = !r.Installed || string.IsNullOrEmpty(r.InstalledVersion) || !string.Equals(r.InstalledVersion, r.RepoVersion, StringComparison.OrdinalIgnoreCase);
            _btnExtInstallUpdate.Enabled = has;
            _btnExtInstallUpdate.Text = needsInstall ? SR.T("Button.Extensions.InstallUpdate", "Install / Update") : SR.T("Button.Extensions.Reinstall", "Reinstall");

            // Enable/Disable
            _btnExtEnableDisable.Enabled = r.Installed;
            _btnExtEnableDisable.Text = r.Enabled ? SR.T("Button.Extensions.Disable", "Disable") : SR.T("Button.Extensions.Enable", "Enable");
        }

        private void DoInstallOrUpdateSelected()
        {
            var r = GetSelectedRow();
            if (r == null)
            {
                Log.Warn("[ExtensionsUI] No row selected.", "Extensions");
                return;
            }
            if (r.ManifestRef == null)
            {
                Log.Error($"[ExtensionsUI] Manifest missing for id={r.Id}", "Extensions");
                MessageBox.Show(this, "Manifest not found", "Extensions",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // 確認ダイアログ
                var msg = r.Installed
                    ? $"Update/Reinstall \"{r.Name}\" to {r.RepoVersion}?"
                    : $"Install \"{r.Name}\" ({r.RepoVersion})?";
                if (MessageBox.Show(this, msg, "Extensions",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                // UIを一時的に無効化（必要なら対象コントロールだけでもOK）
                var prevEnabled = this.Enabled;
                this.Enabled = false;
                try
                {
                    // 進捗ダイアログを表示して実行（キャンセル可）
                    var ok = ExtDownloadDialog.Run(this, r.ManifestRef);
                    if (!ok)
                    {
                        Log.Warn($"[ExtensionsUI] Install canceled or failed: {r.Id}", "Extensions");
                        return;
                    }

                    // 成功時のログと有効化（必要であれば）
                    Log.Info($"[ExtensionsUI] Installed: {r.Id} {r.RepoVersion}", "Extensions");

                    // インストール後に有効化したい場合（状態を明示的にON）
                    try
                    {
                        ExtensionsManager.SetEnabled(r.Id, true);
                    }
                    catch (Exception exSet)
                    {
                        Log.Warn($"[ExtensionsUI] Enable failed: {r.Id} ({exSet.Message})", "Extensions");
                    }
                }
                finally
                {
                    this.Enabled = prevEnabled;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ExtensionsUI] Install failed: {r.Id} ({ex.Message})", "Extensions");
                MessageBox.Show(this, "Install failed:\n" + ex.Message, "Extensions",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 一覧を再読み込みして反映
                ReloadExtensionsIntoGrid();
            }
        }
        private void DoEnableDisableSelected()
        {
            var r = GetSelectedRow();
            if (r == null || !r.Installed) return;

            try
            {
                bool want = !r.Enabled;
                ExtensionsManager.Enable(r.Id, want);
                Log.Info($"{(want ? "Enabled" : "Disabled")}: {r.Id}", "Extensions");
            }
            catch (Exception ex)
            {
                Log.Error($"Toggle failed: {r.Id} ({ex.Message})", "Extensions");
                MessageBox.Show(this, "Operation failed:\n" + ex.Message, "Extensions", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            ReloadExtensionsIntoGrid();
        }

        private void DoUninstallSelected()
        {
            var r = GetSelectedRow();
            if (r == null || !r.Installed) return;

            if (MessageBox.Show(this, $"Uninstall \"{r.Name}\"?", "Extensions",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                ExtensionsManager.Uninstall(r.Id);
                Log.Info($"Uninstalled: {r.Id}", "Extensions");
            }
            catch (Exception ex)
            {
                Log.Error($"Uninstall failed: {r.Id} ({ex.Message})", "Extensions");
                MessageBox.Show(this, "Uninstall failed:\n" + ex.Message, "Extensions", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            ReloadExtensionsIntoGrid();
        }

        private void DoOpenFolderSelected()
        {
            var r = GetSelectedRow();
            if (r == null || !r.Installed) return;

            // 既知の配置規約:
            //  - bin\<id>\<version>
            //  - i18n\<culture>（言語拡張）
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kiritori");
            var candidates = new List<string>();

            if (!string.IsNullOrEmpty(r.InstalledVersion))
                candidates.Add(Path.Combine(root, "bin", r.Id, r.InstalledVersion));

            // 言語拡張（idが "lang_xxx" の場合）
            if (r.Id.StartsWith("lang_", StringComparison.OrdinalIgnoreCase))
            {
                string culture = r.Id.Substring("lang_".Length);
                candidates.Add(Path.Combine(root, "i18n", culture));
            }

            // マニフェストに TargetDir があるなら併用（あれば）
            // var targetDir = GetProp<string>(r.ManifestRef, "Install.TargetDir");
            var targetDir = r.ManifestRef?.Install?.TargetDir;
            if (!string.IsNullOrEmpty(targetDir))
            {
                // ルート相対指定想定（bin/... or i18n/...）
                candidates.Add(Path.Combine(root, targetDir.Replace('/', Path.DirectorySeparatorChar)));
            }

            string open = candidates.FirstOrDefault(Directory.Exists);
            if (string.IsNullOrEmpty(open))
            {
                MessageBox.Show(this, "Install folder not found.", "Extensions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = open, UseShellExecute = true });
            }
            catch { /* ignore */ }
        }

    }
}
