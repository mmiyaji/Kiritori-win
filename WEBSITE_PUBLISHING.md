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

## 構成

紹介ページの元ファイルは `site/` にあり、公開用リポジトリの `sites/kiritori/` と同じ構成です。パスの書き換えは不要です。

- `site/index.html`：日本語の本文。英語は `?lang=en` で表示する。
- `site/privacy.html`：プライバシーポリシー。
- `site/assets/i18n-en.js`：英語の文面。キーは `index.html` の `data-i18n`・`data-img` 属性と対応する。
- `site/assets/site.js`：言語メニュー、ライト／ダーク切り替え、操作動画の切り替え、GLightbox の初期化。
- `site/assets/theme.js`：描画前に保存済みの表示モードを適用する。
- `site/assets/site.css`：スタイル。
- `site/assets/img/`：画面画像（WebP）。日英で内容が違う画像は `-ja` / `-en` を付け、英語表示で自動的に差し替える。同じ画像は接尾辞なし。
- `site/assets/video/`：操作動画とポスター画像。
- `site/assets/glightbox.min.*`：画像の拡大表示（GLightbox、MIT）。

CSS・JavaScript を変更したら、`index.html` の該当する `?v=` を内容のハッシュなどに更新し、ブラウザのキャッシュを避ける。

## 更新手順

1. `Kiritori-win/site/` で紹介ページを編集し、`site/` をルートにしたローカルサーバーで日本語・英語、ライト・ダーク、スマートフォン幅を確認する。
2. `site/` の内容を公開用リポジトリの `sites/kiritori/` にコピーする。アプリのソースやローカル検証レポートを公開対象に混ぜない。不要になった素材は公開側から削除する。
3. `git diff --check`、素材参照、動画切り替え、画像の拡大表示、言語・表示モードの切り替え、FAQの開閉を確認する。
4. 公開用リポジトリの変更をコミットし、承認された範囲で `main` をpushする。
5. Cloudflareダッシュボードで `kiritori` の本番デプロイ成功を確認する。
6. 本番URLで更新内容・動画・使い方・FAQを確認する。

## 紹介ページの方針

明るい背景（ダークモード対応）、簡潔な機能説明、操作動画と実際の画面画像を中心にする。アプリの選択枠（シアン）・アイコンの点線・注釈の番号マーカー（オレンジ）を見た目に使う。概念的なキャッチコピーは置かない。Microsoft Storeを入手先として案内し、ZIP版は推奨・案内しない。詳しい使い方はGitHubへ誘導せず、ページ内の使い方・ショートカット・Tipsで完結させる。FAQ、対応環境、プライバシー、サポート、更新履歴への導線を維持する。

## 2026-10-06の更新

- アプリ側コミット: `9c4291a`
- 公開用ページのコミット: `9e306ba`
- 操作動画は既存素材を使用。キャプチャ・ライブプレビューのポスター画像を追加。
- Cloudflare CLIの保存済み認証は期限切れだったため、Chromeのログイン済みダッシュボードで公開設定を確認した。
- 公開はGit連携を利用し、認証情報やAPIトークンをメモに保存しない。

## 自動公開が起動しない場合の復旧（2026-10-06）

GitHubへのpushが成功しても新規デプロイが作成されない事象を確認。GitHub Appは停止されておらずhomepageのアクセス権もあり、Pagesのmain自動公開と監視パス `*` も有効だった。既存デプロイの再試行は成功したが、元のコミットを使うため最新版への更新にはならない。

承認を得てPagesの設定に `kiritori-main-recovery`（ブランチmain）を追加し、フックのPOST実行で最新コミット `14c7832` の本番公開に成功した。デプロイID: `f742aeac-54a4-4b67-8f84-3282988c0928`。

フックURLはCloudflareダッシュボードで管理する秘密情報。リポジトリには保存しない。実行時は完全な `/pages/webhooks/deploy_hooks/<ID>` URLを使用する。途中までのURLではGit providerエラーとなるため、Git設定不整合と誤認しない。

Gitの自動起動経路の根本原因と復旧は未確認。push後は公開履歴と本番を必ず確認し、反映されない場合はこのmain向けフックで公開できる。
