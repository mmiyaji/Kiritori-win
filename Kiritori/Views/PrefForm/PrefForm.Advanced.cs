using Kiritori.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kiritori.Services.Logging; 

namespace Kiritori
{
    public partial class PrefForm
    {
        // Advanced タブ用 UI
        private DataGridView _gridSettings;
        private BindingList<SettingRow> _rows;
        private Button _btnResetSel, _btnResetAll, _btnReload, _btnExport, _btnImport;
        private Font _valueFontBold;
        private System.Drawing.Color _dirtyBg, _dirtySelBg;
        private System.Drawing.Color _valueBaseBg, _valueBaseSelBg;
        private bool _suppressExternalSync;
        // Settings の 1 行を表現（ValueString を編集対象にする）
        private sealed class SettingRow
        {
            public string Name { get; set; }
            public string Scope { get; set; }          // User / Application
            public string TypeName { get; set; }       // System.String など
            public string TypeDisplay { get; set; }
            public string Description { get; set; }    // (空でも可)
            public bool ReadOnly { get; set; }       // Application-scope 等
            public object DefaultValue { get; set; }   // 既定（型そのもの）
            public object RawValue { get; set; }       // 現在値（型そのもの）
            public object OriginalValue { get; set; }   // ロード直後の値（型そのもの）

            // DataGridView では string として編集 → 変換
            public string ValueString
            {
                get
                {
                    if (RawValue == null) return "";
                    var conv = System.ComponentModel.TypeDescriptor.GetConverter(RawValue.GetType());
                    return conv != null && conv.CanConvertTo(typeof(string))
                        ? (string)conv.ConvertTo(RawValue, typeof(string))
                        : RawValue.ToString();
                }
                set
                {
                    // 文字列を型へ変換
                    Type t;
                    try { t = Type.GetType(TypeName, throwOnError: false); }
                    catch { t = null; }
                    if (t == null) { RawValue = value; return; }

                    var conv = System.ComponentModel.TypeDescriptor.GetConverter(t);
                    if (conv != null && conv.CanConvertFrom(typeof(string)))
                    {
                        try { RawValue = conv.ConvertFromString(value); }
                        catch { /* 無効値はとりあえず文字列として保持 */ RawValue = value; }
                    }
                    else
                    {
                        RawValue = value;
                    }
                }
            }
        }

        /// <summary>Advanced タブに表を構築</summary>
        private void BuildAdvancedTab()
        {
            // 強調用
            _valueFontBold = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);

            // Value列のベース色（常時）
            _valueBaseBg    = System.Drawing.Color.FromArgb(242, 248, 255);
            _valueBaseSelBg = System.Drawing.Color.FromArgb(220, 235, 250);

            // 未保存の強調色
            _dirtyBg    = System.Drawing.Color.FromArgb(255, 253, 213);
            _dirtySelBg = System.Drawing.Color.FromArgb(255, 236, 147);

            // 画面ロード → 値読み込み
            ReloadSettingsIntoGrid();

            // Settings の外部変更を監視
            HookSettingsEvents();

            // フォームを閉じたら購読解除（メモリリーク防止）
            this.FormClosed += (s, e) => UnhookSettingsEvents();

            // ルートレイアウト
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.tabAdvanced.Controls.Clear();
            this.tabAdvanced.Controls.Add(root);

            // ヘルプパネル
            var helpLabel = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0),
                Padding = new Padding(2, 0, 0, 0),
                Text = SR.T("Text.Advanced.Tips", "Tip: Double-click the Value cell to edit. Press Enter to commit changes."),
                Tag = "loc:Text.Advanced.Tips"
            };
            root.Controls.Add(helpLabel, 0, 0);

            // DataGridView
            _gridSettings = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };

            _gridSettings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; // 明示
            _gridSettings.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

            // Name（固定）
            var colName = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SettingRow.Name),
                HeaderText = SR.T("Column.Advanced.Key", "Key"),
                ReadOnly = true,
                Width = 150,
                Tag = "loc:Column.Advanced.Key",
                // MinimumWidth = 160
            };
            // Scope（固定）
            var colScope = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SettingRow.Scope),
                HeaderText = SR.T("Column.Advanced.Scope", "Scope"),
                ReadOnly = true,
                Width = 50,
                Tag = "loc:Column.Advanced.Scope",
                // MinimumWidth = 70
            };
            // Type（固定）
            var colType = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SettingRow.TypeDisplay),
                HeaderText = SR.T("Column.Advanced.Type", "Type"),
                ReadOnly = true,
                Width = 50,
                Tag = "loc:Column.Advanced.Type",
                // MinimumWidth = 140
            };
            // Value（可変：最優先で広く取る）
            var colValue = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SettingRow.ValueString),
                HeaderText = SR.T("Column.Advanced.Value", "Value(editable)"),
                ReadOnly = false,
                Width = 110,
                Tag = "loc:Column.Advanced.Value",
            };
            // Value 列の既定スタイル（常時薄色）
            colValue.HeaderCell.ToolTipText = SR.T("Column.Advanced.Value.Editable", "This is the only editable column");
            colValue.DefaultCellStyle.BackColor = _valueBaseBg;
            colValue.DefaultCellStyle.SelectionBackColor = _valueBaseSelBg;
            colValue.DefaultCellStyle.SelectionForeColor = SystemColors.ControlText;
            colValue.DefaultCellStyle.Font = SystemFonts.MessageBoxFont;
            // Description（可変）
            var colDesc = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SettingRow.Description),
                HeaderText = SR.T("Column.Advanced.Description", "Description"),
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 40,       // ← Value:Description = 60:40 の比率
                Tag = "loc:Column.Advanced.Description",
                // MinimumWidth = 120
            };
            _gridSettings.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                var isValueCol = _gridSettings.Columns[e.ColumnIndex].DataPropertyName == nameof(SettingRow.ValueString);
                if (!isValueCol) return;

                var r = _gridSettings.Rows[e.RowIndex].DataBoundItem as SettingRow;
                if (r == null) return;

                // “未保存”＝この画面の RawValue と 現在の保存値(Settings.Default) が不一致
                bool dirty = IsRowDirty(r);

                if (dirty)
                {
                    e.CellStyle.Font = _valueFontBold;
                    e.CellStyle.BackColor = _dirtyBg;
                    e.CellStyle.SelectionBackColor = _dirtySelBg;
                    e.CellStyle.SelectionForeColor = SystemColors.ControlText;
                }
                else
                {
                    e.CellStyle.Font = SystemFonts.MessageBoxFont;
                    e.CellStyle.BackColor = _valueBaseBg;          // 常時薄色
                    e.CellStyle.SelectionBackColor = _valueBaseSelBg;
                    e.CellStyle.SelectionForeColor = SystemColors.ControlText;
                }
            };
            _gridSettings.CellEndEdit += (s, e) =>
            {
                if (e.RowIndex >= 0 &&
                    _gridSettings.Columns[e.ColumnIndex].DataPropertyName == nameof(SettingRow.ValueString))
                {
                    _gridSettings.InvalidateRow(e.RowIndex);
                    RecomputeDirtyFromAdvancedRows();
                }
            };
            _gridSettings.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 &&
                    _gridSettings.Columns[e.ColumnIndex].DataPropertyName == nameof(SettingRow.ValueString))
                {
                    _gridSettings.InvalidateRow(e.RowIndex);
                    RecomputeDirtyFromAdvancedRows();
                }
            };

            _gridSettings.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var target = _gridSettings.Rows[e.RowIndex].Cells[colValue.Index];
                _gridSettings.CurrentCell = target;
                _gridSettings.BeginEdit(true);
            };
            _gridSettings.Columns.AddRange(new DataGridViewColumn[] {
                colName,
                // colScope,
                colType,
                colValue,
                colDesc,
                });

            _gridSettings.ShowCellToolTips = true;
            _gridSettings.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var row = _gridSettings.Rows[e.RowIndex].DataBoundItem as SettingRow;
                if (row == null) return;
                var col = _gridSettings.Columns[e.ColumnIndex];
                if (col.DataPropertyName == nameof(SettingRow.TypeDisplay))
                {
                    // アセンブリ完全修飾名（長い方）をツールチップで
                    e.ToolTipText = row.TypeName ?? "";
                }
            };
            // 文字が詰まって見えない対策（任意）
            _gridSettings.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _gridSettings.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            // _gridSettings.Columns[colValue.Index].DefaultCellStyle.Font =
            //     new Font(SystemFonts.MessageBoxFont, FontStyle.Regular);

            _gridSettings.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    var row = _gridSettings.Rows[e.RowIndex].DataBoundItem as SettingRow;
                    if (row != null)
                    {
                        var isValueCol = _gridSettings.Columns[e.ColumnIndex].DataPropertyName == nameof(SettingRow.ValueString);
                        if (isValueCol && row.ReadOnly)
                        {
                            e.CellStyle.BackColor = SystemColors.ControlLight;
                            e.CellStyle.ForeColor = SystemColors.GrayText;
                        }
                    }
                }
            };

            // 編集禁止行のブロック
            _gridSettings.CellBeginEdit += (s, e) =>
            {
                var row = _gridSettings.Rows[e.RowIndex].DataBoundItem as SettingRow;
                if (row != null && row.ReadOnly)
                {
                    e.Cancel = true;
                    System.Media.SystemSounds.Asterisk.Play();
                }
            };

            root.Controls.Add(_gridSettings, 0, 0);

            // ボタン行
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0)
            };

            _btnResetSel = new Button {
                Text = "Reset (Selected)",
                Tag = "loc:Button.Advanced.ResetSelected",
                AutoSize = true };
            _btnResetAll = new Button {
                Text = "Reset (All)",
                Tag = "loc:Button.Advanced.ResetAll",
                AutoSize = true };
            _btnReload = new Button {
                Text = "Reload",
                Tag = "loc:Button.Advanced.Reload",
                AutoSize = true };
            _btnExport = new Button {
                Text = "Export...",
                Tag = "loc:Button.Advanced.Export",
                AutoSize = true };
            _btnImport = new Button {
                Text = "Import...",
                Tag = "loc:Button.Advanced.Import",
                AutoSize = true };

            _btnResetSel.Click += (s, e) => ResetSelectedToDefault();
            _btnResetAll.Click += (s, e) => ResetAllToDefault();
            _btnReload.Click += (s, e) => ReloadSettingsIntoGrid();
            _btnExport.Click += (s, e) => ExportToFile();
            _btnImport.Click += (s, e) => ImportFromFile();

            buttons.Controls.AddRange(new Control[] { _btnResetSel, _btnResetAll, _btnReload, _btnExport, _btnImport });
            root.Controls.Add(buttons, 0, 2);

        }

        // 設定キーから説明文字列を引くヘルパ
        private string GetSettingDescription(string key, SettingsProperty p)
        {
            // 1) リソース（お好みで順序は調整可）
            string s;
            s = SR.T("Setting.Display." + key, null); if (!string.IsNullOrEmpty(s)) return s;
            // s = SR.T("SettingDesc_" + key, null); if (!string.IsNullOrEmpty(s)) return s;
            // s = SR.T("Setting_Display_" + key, null); if (!string.IsNullOrEmpty(s)) return s;
            // s = SR.T("Text_" + key, null); if (!string.IsNullOrEmpty(s)) return s;

            // 2) Settings の DescriptionAttribute
            var da = p.Attributes[typeof(DescriptionAttribute)] as DescriptionAttribute;
            if (da != null && !string.IsNullOrEmpty(da.Description)) return da.Description;

            return string.Empty;
        }

        private static bool ValueEquals(object a, object b, string typeName)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Equals(b)) return true;

            try
            {
                var t = Type.GetType(typeName, throwOnError: false);
                var conv = (t != null) ? TypeDescriptor.GetConverter(t) : null;
                string sa = (conv != null && conv.CanConvertTo(typeof(string)))
                    ? conv.ConvertToInvariantString(a) : a.ToString();
                string sb = (conv != null && conv.CanConvertTo(typeof(string)))
                    ? conv.ConvertToInvariantString(b) : b.ToString();
                return string.Equals(sa, sb, StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private bool IsRowDirty(SettingRow r)
        {
            object persisted = null;
            try { persisted = Properties.Settings.Default[r.Name]; } catch { }
            return !ValueEquals(r.RawValue, persisted, r.TypeName);
        }



        private static string GetFriendlyTypeName(Type t)
        {
            if (t == null) return "";
            // C# の代表的なエイリアス
            var map = new System.Collections.Generic.Dictionary<Type, string>
            {
                { typeof(void), "void" }, { typeof(bool), "bool" }, { typeof(byte), "byte" },
                { typeof(sbyte), "sbyte" }, { typeof(char), "char" }, { typeof(decimal), "decimal" },
                { typeof(double), "double" }, { typeof(float), "float" }, { typeof(int), "int" },
                { typeof(uint), "uint" }, { typeof(long), "long" }, { typeof(ulong), "ulong" },
                { typeof(short), "short" }, { typeof(ushort), "ushort" }, { typeof(string), "string" },
                { typeof(object), "object" }
            };
            if (map.TryGetValue(t, out var alias)) return alias;

            // Nullable<T> → T?
            if (Nullable.GetUnderlyingType(t) is Type nt) return GetFriendlyTypeName(nt) + "?";

            // 配列
            if (t.IsArray) return GetFriendlyTypeName(t.GetElementType()) + "[]";

            // ジェネリック（List<int> など）
            if (t.IsGenericType)
            {
                var name = t.Name;
                var tick = name.IndexOf('`');
                if (tick >= 0) name = name.Substring(0, tick);
                var args = t.GetGenericArguments().Select(GetFriendlyTypeName);
                return $"{name}<{string.Join(", ", args)}>";
            }

            // System.Drawing.Color → Color / Size など、末尾名を優先
            // 名前空間は隠す（必要なら "Color (System.Drawing)" にしてもOK）
            return t.Name;
        }

        private void ReloadSettingsIntoGrid()
        {
            var S = Properties.Settings.Default;
            var coll = S.Properties;

            var list = new List<SettingRow>();
            foreach (SettingsProperty p in coll)
            {
                // Scope
                var scope = p.Attributes[typeof(System.Configuration.UserScopedSettingAttribute)] != null
                    ? "User" : "Application";

                // Description
                // string desc = "";
                // var descAttr = p.Attributes[typeof(System.ComponentModel.DescriptionAttribute)] as DescriptionAttribute;
                // Log.Debug($"Description for {p.Name}: {descAttr}");
                // if (descAttr != null) desc = descAttr.Description ?? "";
                string desc = GetSettingDescription(p.Name, p);
                // 型が null の場合に備えてフォールバック
                var pt = p.PropertyType ?? typeof(string);

                // 既定値の型変換（安全に）
                object def = p.DefaultValue;
                try
                {
                    if (def is string ds)
                    {
                        var conv = TypeDescriptor.GetConverter(pt);
                        if (conv != null && conv.CanConvertFrom(typeof(string)))
                            def = conv.ConvertFromString(ds);
                    }
                }
                catch { /* 既定値の変換失敗は無視 */ }

                // 現在値（安全に）
                object cur = null;
                try { cur = S[p.Name]; } catch { /* 存在しない/型不一致は null 扱い */ }

                list.Add(new SettingRow
                {
                    Name         = p.Name,
                    Scope        = scope,
                    TypeName     = pt.AssemblyQualifiedName ?? pt.FullName ?? pt.Name, // ← null防止
                    TypeDisplay  = GetFriendlyTypeName(pt),
                    Description  = desc,
                    ReadOnly     = scope != "User",
                    DefaultValue = def,
                    RawValue     = cur,
                    OriginalValue= cur    // 外部同期の基準
                });
            }

            _rows = new BindingList<SettingRow>(list.OrderBy(r => r.Name).ToList());
            Log.Debug($"Loaded {_rows.Count} settings into Advanced tab", "Advanced");
            // foreach (var r in _rows)
            // {
            //     Log.Debug($"  {r.Name} = {r.ValueString} (Default={r.DefaultValue}, Type={r.TypeName}, ReadOnly={r.ReadOnly})");
            // }

            // Grid がまだ未構築のタイミングで呼ばれても落ちないように
            if (_gridSettings != null)
                _gridSettings.DataSource = _rows;
        }


        /// <summary>Advanced テーブルの編集結果を Settings に反映（OK 押下時用）</summary>
        private void ApplyAdvancedEditsToSettings()
        {
            if (_rows == null) return;
            var S = Properties.Settings.Default;

            // --- HistoryLimit は decimal → int に明示変換して保存 ---
            int limit = Decimal.ToInt32(this.textBoxHistory.Value);
            if (limit < 0) limit = 0;
            S.HistoryLimit = limit;

            _suppressExternalSync = true;   // ← 自画面からの書き換え通知は無視
            try
            {
                foreach (var r in _rows)
                {
                    if (r.ReadOnly) continue;

                    if (string.Equals(r.Name, nameof(S.HistoryLimit), StringComparison.Ordinal)) continue;

                    try
                    {
                        // 設定プロパティの型に合わせて値を整形して代入
                        var prop = S.Properties[r.Name];
                        if (prop == null) continue;

                        var targetType = prop.PropertyType;
                        object value = CoerceToType(r.RawValue ?? r.ValueString, targetType);
                        S[r.Name] = value;
                    }
                    catch
                    {
                        // 型がどうしても合わない時はスキップ（落とさない）
                    }
                }
            }
            finally
            {
                _suppressExternalSync = false;
            }

            // 保存後、最新保存値を基準値に更新（未保存強調をリセット）
            foreach (var r in _rows)
            {
                if (string.Equals(r.Name, nameof(S.HistoryLimit), StringComparison.Ordinal))
                    r.OriginalValue = S.HistoryLimit;
                else
                    r.OriginalValue = S[r.Name];
            }
            _gridSettings?.Invalidate();
        }

        // 汎用：設定の型へ安全に変換
        private static object CoerceToType(object src, Type targetType)
        {
            if (src == null) return null;

            // 文字列 → 目標型
            if (src is string s)
            {
                if (targetType == typeof(string)) return s;
                if (targetType == typeof(int))    return int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (targetType == typeof(decimal))return decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (targetType == typeof(bool))   return bool.Parse(s);
                if (targetType.IsEnum)            return Enum.Parse(targetType, s, ignoreCase: true);
                // その他は ChangeType にフォールバック
                return Convert.ChangeType(s, targetType, System.Globalization.CultureInfo.InvariantCulture);
            }

            // NumericUpDown など decimal → 各数値型
            if (src is decimal d)
            {
                if (targetType == typeof(int))     return Decimal.ToInt32(d);
                if (targetType == typeof(long))    return Decimal.ToInt64(d);
                if (targetType == typeof(float))   return (float)d;
                if (targetType == typeof(double))  return (double)d;
                if (targetType == typeof(decimal)) return d;
            }

            // 既に互換ならそのまま
            if (targetType.IsAssignableFrom(src.GetType())) return src;

            // 最後の手段
            return Convert.ChangeType(src, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }
        private void HookSettingsEvents()
        {
            var S = Properties.Settings.Default;
            S.PropertyChanged += Settings_PropertyChanged; // 個別のプロパティ変更
            // SettingsSaving/SettingsLoaded を使う場合は必要に応じて追記
        }
        private void UnhookSettingsEvents()
        {
            var S = Properties.Settings.Default;
            S.PropertyChanged -= Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressExternalSync || _rows == null || string.IsNullOrEmpty(e.PropertyName)) return;

            var row = _rows.FirstOrDefault(r => r.Name == e.PropertyName);
            if (row == null) return;

            var persisted = Properties.Settings.Default[e.PropertyName];

            // ユーザーがこの画面で未編集（Raw == Original）なら、外部保存をそのまま取り込む
            if (ValueEquals(row.RawValue, row.OriginalValue, row.TypeName))
            {
                row.RawValue = persisted;      // 表示値を外部保存に追従
                row.OriginalValue = persisted; // 基準値も更新
                InvalidateRowByName(e.PropertyName);
            }
            else
            {
                // すでにこの画面で編集済みなら、“未保存強調”のまま（ユーザー編集を優先）
                // 視覚的には dirty（太字＋濃色）のままなので特別処理は不要
            }
        }

        private void InvalidateRowByName(string name)
        {
            for (int i = 0; i < _gridSettings.Rows.Count; i++)
            {
                var r = _gridSettings.Rows[i].DataBoundItem as SettingRow;
                if (r != null && r.Name == name)
                {
                    _gridSettings.InvalidateRow(i);
                    break;
                }
            }
        }
        // 画面の行と保存済み Settings を比較して Dirty を再計算
        private void RecomputeDirtyFromAdvancedRows()
        {
            if (_rows == null) return;

            bool anyDirty = false;
            var S = Properties.Settings.Default;

            foreach (var r in _rows)
            {
                // 読み取り専用行は無視（ユーザーが保存対象にできない）
                if (r.ReadOnly) continue;

                object persisted = null;
                try { persisted = S[r.Name]; } catch { /* 無効キー等は差分ありとみなす */ }

                if (!ValueEquals(r.RawValue, persisted, r.TypeName))
                {
                    anyDirty = true;
                    break;
                }
            }

            if (_suppressDirty > 0) return; // リロード中などは無視

            _isDirty = anyDirty;
            UpdateDirtyUI();
        }

        private void ResetSelectedToDefault()
        {
            if (_gridSettings.CurrentRow == null) return;
            var r = _gridSettings.CurrentRow.DataBoundItem as SettingRow;
            if (r == null || r.ReadOnly) return;
            r.RawValue = r.DefaultValue;
            // 画面更新
            var idx = _gridSettings.CurrentRow.Index;
            _gridSettings.InvalidateRow(idx);
            _gridSettings.Refresh();
            RecomputeDirtyFromAdvancedRows();
        }

        private void ResetAllToDefault()
        {
            foreach (var r in _rows)
            {
                if (!r.ReadOnly) r.RawValue = r.DefaultValue;
            }
            _gridSettings.Refresh();
            RecomputeDirtyFromAdvancedRows();
        }

        private void ExportToFile()
        {
            using (var sfd = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "Kiritori.Settings.Export.json" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                var kv = _rows.ToDictionary(r => r.Name, r => r.ValueString);
                var json = SimpleJson.Serialize(kv); // 下の最小JSONヘルパを使用
                System.IO.File.WriteAllText(sfd.FileName, json);
            }
        }

        private void ImportFromFile()
        {
            using (var ofd = new OpenFileDialog { Filter = "JSON (*.json)|*.json" })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                var json = System.IO.File.ReadAllText(ofd.FileName);
                var dict = SimpleJson.Deserialize(json);
                foreach (var r in _rows)
                {
                    if (r.ReadOnly) continue;
                    if (dict.TryGetValue(r.Name, out var str))
                    {
                        r.ValueString = str; // ここで型変換されて RawValue に反映
                    }
                }
                _gridSettings.Refresh();
                RecomputeDirtyFromAdvancedRows();
            }
        }

        // 依存の無い超軽量 JSON（辞書<string,string> 専用）
        private static class SimpleJson
        {
            public static string Serialize(System.Collections.Generic.IDictionary<string, string> d)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                bool first = true;
                foreach (var kv in d)
                {
                    if (!first) sb.AppendLine(",");
                    first = false;

                    sb.Append("  \"").Append(E(kv.Key)).Append("\": ");
                    sb.Append("\"").Append(E(kv.Value ?? "")).Append("\"");
                }
                sb.AppendLine();
                sb.Append("}");
                return sb.ToString();

                string E(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }


            public static Dictionary<string, string> Deserialize(string json)
            {
                // 最低限のキー/値取り出し（フォーマルな JSON である前提）
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                if (string.IsNullOrWhiteSpace(json)) return result;

                // ごく簡易なパース
                // 例: {"A":"1","B":"hello"}
                int i = 0;
                SkipWs(); Expect('{'); SkipWs();
                if (Peek() == '}') { i++; return result; }
                while (true)
                {
                    SkipWs(); var key = ReadString(); SkipWs(); Expect(':'); SkipWs();
                    var val = ReadString(); SkipWs();
                    result[key] = val;
                    if (Peek() == ',') { i++; continue; }
                    if (Peek() == '}') { i++; break; }
                    break;
                }
                return result;

                char Peek() => i < json.Length ? json[i] : '\0';
                void Expect(char c) { if (Peek() == c) { i++; } }
                void SkipWs() { while (char.IsWhiteSpace(Peek())) i++; }
                string ReadString()
                {
                    Expect('\"');
                    var sb = new System.Text.StringBuilder();
                    while (true)
                    {
                        var ch = Peek(); i++;
                        if (ch == '\0') break;
                        if (ch == '\"') break;
                        if (ch == '\\')
                        {
                            var esc = Peek(); i++;
                            if (esc == '\"') sb.Append('\"');
                            else if (esc == '\\') sb.Append('\\');
                            else sb.Append(esc);
                        }
                        else sb.Append(ch);
                    }
                    return sb.ToString();
                }
            }
        }
    }
}
