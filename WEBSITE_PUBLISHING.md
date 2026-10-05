# Kiritori紹介ページの公開手順

## 公開先

- 本番URL: https://kiritori.ruhenheim.org/
- Cloudflare Pagesプロジェクト: `kiritori`
- Pages標準URL: https://kiritori.pages.dev/
- Cloudflareアカウント: `Everfree1987@gmail.com` のアカウント
- 公開元Gitリポジトリ: https://github.com/mmiyaji/homepage
- 本番ブランチ: `main`（pushにより自動公開）
- 公開対象ディレクトリ: `sites/kiritori`
- 手元の公開用チェックアウト: `C:\Users\mail\Documents\git\homepage`

アプリのリポジトリ `mmiyaji/Kiritori-win` にpushするだけでは、このPagesプロジェクトは更新されません。CloudflareはDNSだけでなくPagesでこの紹介ページをホストしています。

## 更新手順

1. `Kiritori-win/index.html` で紹介ページを編集し、ローカルブラウザで確認する。
2. 公開用リポジトリの `sites/kiritori/index.html` に反映する。
3. 素材パスを公開用に変更する。
   - `KiritoriPackage/Images/icon_256x256.png` → `assets/icon.png`
   - `screenshot/` → `assets/`（JavaScript内の動画切り替えパスも含む）
   - OGP画像には本番URLを使用する。
   - 公開用ページの `assets/icon.png` のfaviconを維持する。
4. 追加・更新した素材を `sites/kiritori/assets/` にコピーする。アプリのソースやローカル検証レポートを公開対象に混ぜない。
5. `git diff --check`、素材参照、動画切り替え、ガイド・FAQの開閉、スマートフォン幅での表示を確認する。
6. 公開用リポジトリの変更をコミットし、承認された範囲で `main` をpushする。
7. Cloudflareダッシュボードで `kiritori` の本番デプロイ成功を確認する。
8. 本番URLで更新内容・動画・使い方・FAQを確認する。

## 紹介ページの方針

白地、簡潔な機能説明、操作動画を主役にする。概念的なキャッチコピーは置かない。Microsoft Storeを入手先として案内し、ZIP版は推奨・案内しない。詳しい使い方はGitHubへ誘導せず、ページ内の折りたたみガイドで完結させる。FAQ、対応環境、プライバシー、サポート、更新履歴への導線を維持する。

## 2026-10-06の更新

- アプリ側コミット: `9c4291a`
- 公開用ページのコミット: `9e306ba`
- 操作動画は既存素材を使用。キャプチャ・ライブプレビューのポスター画像を追加。
- Cloudflare CLIの保存済み認証は期限切れだったため、Chromeのログイン済みダッシュボードで公開設定を確認した。
- 公開はGit連携を利用し、認証情報やAPIトークンをメモに保存しない。
