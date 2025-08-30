using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications;
using Kiritori.Helpers;
using Kiritori.Services.Notifications;
using Kiritori.Services.Ocr;
using Windows.UI.Notifications;

namespace Kiritori
{
    public partial class SnapWindow : Form
    {
        #region ===== 設定読み込み / 監視 =====
        private void ReadSettingsIntoFieldsWithFallback()
        {
            var S = Properties.Settings.Default;

            isWindowShadow = S.isWindowShadow;
            isAfloatWindow = S.isAfloatWindow;
            isOverlay = S.isOverlay;
            WindowAlphaPercent = S.WindowAlphaPercent / 100.0;
            isHighlightOnHover = S.isHighlightWindowOnHover;

            var c = S.HoverHighlightColor;
            int a = S.HoverHighlightAlphaPercent;
            int t = S.HoverHighlightThickness;

            if (c.IsEmpty) c = Color.Red;
            if (a <= 0) a = 60;
            if (t <= 0) t = 2;

            _hoverColor = c;
            _hoverAlphaPercent = Math.Max(0, Math.Min(100, a));
            _hoverThicknessPx = Math.Max(1, t);
        }

        private void ApplyUiFromFields()
        {
            if (!this.IsHandleCreated) return;
            try
            {
                this.TopMost = isAfloatWindow;
                this.Opacity = WindowAlphaPercent;
            }
            catch { }
        }

        private void SafeApplySettings()
        {
            if (_isApplyingSettings) return;
            _isApplyingSettings = true;
            try
            {
                if (this.IsDisposed || this.Disposing) return;

                ReadSettingsIntoFieldsWithFallback();
                ApplyUiFromFields();

                pictureBox1?.Invalidate();
            }
            finally
            {
                _isApplyingSettings = false;
            }
        }

        private void HookSettingsChanged()
        {
            if (_settingsHandler != null)
                Properties.Settings.Default.PropertyChanged -= _settingsHandler;

            _settingsHandler = (s, e) =>
            {
                if (this.IsDisposed || this.Disposing) return;

                if (this.InvokeRequired)
                {
                    try { this.BeginInvoke(_settingsHandler, s, e); } catch { }
                    return;
                }

                if (string.IsNullOrEmpty(e.PropertyName) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightColor) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightAlphaPercent) ||
                    e.PropertyName == nameof(Properties.Settings.Default.HoverHighlightThickness) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isHighlightWindowOnHover) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isWindowShadow) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isAfloatWindow) ||
                    e.PropertyName == nameof(Properties.Settings.Default.isOverlay) ||
                    e.PropertyName == nameof(Properties.Settings.Default.WindowAlphaPercent))
                {
                    SafeApplySettings();
                }
            };

            Properties.Settings.Default.PropertyChanged += _settingsHandler;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyUiFromFields();
        }

        #endregion

    }
}