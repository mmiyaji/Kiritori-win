using System;
using System.Linq;
using System.Windows.Forms;
using Kiritori.Services.Logging;

namespace Kiritori.Services.Extensions
{
    internal static class ExtensionsAuto
    {
        /// <summary>
        /// 指定IDの拡張を未導入なら導入（必要なら確認ダイアログ）。
        /// </summary>
        public static bool TryEnsure(string id, IWin32Window owner = null, bool prompt = true, bool throwOnDecline = false)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (ExtensionsManager.IsInstalled(id)) return true;

            var manifest = ExtensionsManager.LoadRepoManifests()
                                            .FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
            if (manifest == null)
            {
                Log.Warn($"[AutoInstall] Manifest not found: {id}", "Extensions");
                return false;
            }

            if (prompt)
            {
                var r = MessageBox.Show(owner,
                    $"拡張機能「{manifest.DisplayName ?? manifest.Id}」({manifest.Version}) をインストールしますか？",
                    "Kiritori Extensions", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes)
                {
                    if (throwOnDecline) throw new OperationCanceledException("User declined installing " + id);
                    return false;
                }
            }

            try
            {
                ExtensionsManager.Install(manifest);
                Log.Info($"[AutoInstall] Installed: {manifest.Id} {manifest.Version}", "Extensions");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoInstall] Install failed: {manifest.Id} ({ex.Message})", "Extensions");
                MessageBox.Show(owner, "インストールに失敗しました:\n" + ex.Message,
                    "Kiritori Extensions", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
