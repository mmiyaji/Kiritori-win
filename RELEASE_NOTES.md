# Kiritori v1.8.0 Release Notes / リリースノート

Release date / リリース日: 2026-10-12

## 日本語

v1.8.0 では、キャプチャ画像への注釈と画像加工を追加しました。

### 新機能

- `Ctrl`+`E` または右クリックメニューの 編集 → 注釈を編集 から、矩形、矢印、テキスト、番号付きマーカー、部分モザイクを追加できます。色、線のスタイル、幅、文字サイズとフォントを調整でき、操作の取り消し・やり直しにも対応しました。
- 編集 → 画像効果 から、キャプチャ画像全体にモザイク、セピア、グレースケールを適用できます。モザイクは3段階で調整でき、元画像を保ったまま表示を切り替えられます。
- ライブキャプチャにも、加工なし、モザイク、セピア、グレースケールを適用できます。モザイクは3段階で調整でき、選択中の効果は録画にも反映されます。
- ライブプレビューのタイトルバーを初期状態で非表示にし、設定を次回起動にも引き継ぐようにしました。
- GIF 録画のメモリ使用量に上限を設け、録画処理の競合とウィンドウ終了時のリソース解放を改善しました。
- 注釈ツールバー、SVG アイコン、注釈色の選択とカスタムカラーを追加しました。

### 修正

- 画像の拡大縮小時に注釈の位置と範囲がずれる問題を修正しました。
- 注釈ツールやウィンドウのフォーカスを切り替えたときにカスタム色を保持するようにしました。
- ライブキャプチャ画像を通常のキャプチャウィンドウへ引き継ぐ際の初期フレーム処理を修正しました。

## English

Version 1.8.0 adds annotations and image effects for captured images.

### New features

- Press `Ctrl`+`E` or choose Edit → Edit annotations to add rectangles, arrows, text, numbered markers, and pixelation. Customize colors, line styles, stroke width, font size, and font family; undo and redo are supported.
- Choose Edit → Image effects to apply mosaic, sepia, or grayscale to a captured image. Mosaic has three strength levels, and the original image is preserved when switching effects.
- Live capture also supports mosaic, sepia, and grayscale, with three mosaic strengths. The selected effect is included in recordings.
- The live preview title bar is hidden by default, and the setting is preserved across launches.
- Added a memory limit for GIF recording and improved recording concurrency and resource cleanup when windows close.
- Added an annotation toolbar, SVG icons, color presets, and custom annotation colors.

### Fixes

- Fixed annotation alignment and bounds when a captured image is resized.
- Preserved custom annotation colors when changing tools or window focus.
- Fixed initial-frame handling when transferring a live capture into a regular capture window.
