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
        #region ===== キー入力（ホットキー） =====
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch ((int)keyData)
            {
                case (int)HOTS.MOVE_LEFT:
                    this.SetDesktopLocation(this.Location.X - MOVE_STEP, this.Location.Y);
                    break;
                case (int)HOTS.MOVE_RIGHT:
                    this.SetDesktopLocation(this.Location.X + MOVE_STEP, this.Location.Y);
                    break;
                case (int)HOTS.MOVE_UP:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y - MOVE_STEP);
                    break;
                case (int)HOTS.MOVE_DOWN:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y + MOVE_STEP);
                    break;
                case (int)HOTS.SHIFT_MOVE_LEFT:
                    this.SetDesktopLocation(this.Location.X - SHIFT_MOVE_STEP, this.Location.Y);
                    break;
                case (int)HOTS.SHIFT_MOVE_RIGHT:
                    this.SetDesktopLocation(this.Location.X + SHIFT_MOVE_STEP, this.Location.Y);
                    break;
                case (int)HOTS.SHIFT_MOVE_UP:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y - SHIFT_MOVE_STEP);
                    break;
                case (int)HOTS.SHIFT_MOVE_DOWN:
                    this.SetDesktopLocation(this.Location.X, this.Location.Y + SHIFT_MOVE_STEP);
                    break;

                case (int)HOTS.SHADOW:
                    ToggleShadow(!this.WindowShadowEnabled);
                    break;
                case (int)HOTS.FLOAT:
                    afloatImage(this);
                    break;
                case (int)HOTS.HOVER:
                    ToggleHoverHighlight(!this.isHighlightOnHover);
                    break;

                case (int)HOTS.ESCAPE:
                case (int)HOTS.CLOSE:
                    closeImage(this);
                    break;

                case (int)HOTS.SAVE:
                    saveImage();
                    break;
                case (int)HOTS.LOAD:
                    loadImage();
                    break;
                case (int)HOTS.OPEN:
                    openImage();
                    break;
                case (int)HOTS.EDIT_MSPAINT:
                    editInMSPaint(this);
                    break;

                case (int)HOTS.ZOOM_ORIGIN_NUMPAD:
                case (int)HOTS.ZOOM_ORIGIN_MAIN:
                    zoomOff();
                    break;
                case (int)HOTS.ZOOM_IN:
                    zoomIn();
                    break;
                case (int)HOTS.ZOOM_OUT:
                    zoomOut();
                    break;
                case (int)HOTS.LOCATE_ORIGIN_MAIN:
                    initLocation();
                    break;

                case (int)HOTS.PASTE:
                    openClipboard();
                    break;
                case (int)HOTS.COPY:
                    copyImage(this);
                    break;
                case (int)HOTS.CUT:
                    closeImage(this);
                    break;
                case (int)HOTS.OCR:
                    RunOcrOnCurrentImage();
                    break;
                case (int)HOTS.PRINT:
                    printImage();
                    break;
                case (int)HOTS.MINIMIZE:
                    minimizeWindow();
                    break;
                case (int)HOTS.SETTING:
                    PrefForm.ShowSingleton(this.ma);
                    break;
                // case (int)HOTS.EXIT:
                //     exitApp();
                //     break;

                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }

        #endregion

    }
}