using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Kiritori.Services.History
{
    public static class HistoryBridge
    {
        private static readonly object _gate = new object();
        private static Func<IList<HistoryEntry>> _provider;
        public static event EventHandler HistoryChanged;

        public static void SetProvider(Func<IList<HistoryEntry>> provider)
        {
            lock (_gate) { _provider = provider; }
        }

        public static IList<HistoryEntry> GetSnapshot()
        {
            Func<IList<HistoryEntry>> f;
            lock (_gate) { f = _provider; }
            if (f != null)
            {
                try { var list = f(); return (list != null) ? new List<HistoryEntry>(list) : new List<HistoryEntry>(); }
                catch { }
            }
            return new List<HistoryEntry>();
        }

        public static void TryBindFromOpenForms()
        {
            if (_provider != null) return;
            var main = Application.OpenForms.OfType<MainApplication>().FirstOrDefault();
            if (main != null) SetProvider(() => main.GetHistoryEntriesSnapshot());
        }
        // public static void RaiseChanged(object sender = null)
        // {
        //     var h = HistoryChanged;
        //     if (h != null) h(sender ?? typeof(HistoryBridge), EventArgs.Empty);
        // }
        public static void RaiseChanged(object sender) => HistoryChanged?.Invoke(sender, EventArgs.Empty);

        private static WeakReference<MainApplication> _mainRef;
        private static SynchronizationContext _uiCtx;
        public static void RegisterMain(MainApplication main)
        {
            _mainRef = (main != null) ? new WeakReference<MainApplication>(main) : null;
            Log.Debug($"HB.RegisterMain: main={(main!=null)}", "History");
        }
        
        public static void RegisterUiContext(SynchronizationContext ctx)
        {
            _uiCtx = ctx;
            Log.Debug($"HB.RegisterUiContext: ctx={(ctx!=null)}", "History");
        }

        private static MainApplication TryGetMain()
        {
            if (_mainRef != null && _mainRef.TryGetTarget(out var m) && m != null && !m.IsDisposed)
                return m;

            // 最後の保険：OpenForms から探して再登録
            var fallback = Application.OpenForms.OfType<MainApplication>().FirstOrDefault();
            if (fallback != null) RegisterMain(fallback);
            return fallback;
        }

        public static void RequestDelete(IEnumerable<HistoryEntry> entries)
        {
            var list = (entries ?? Enumerable.Empty<HistoryEntry>()).Where(e => e != null).ToList();
            Log.Debug($"HB.RequestDelete: count={list.Count}", "History");

            var main = TryGetMain();
            Log.Debug($"HB.RequestDelete: mainFound={(main!=null)}", "History");
            if (main == null || main.IsDisposed) return;

            void Do()
            {
                try
                {
                    Log.Debug("HB.RequestDelete: invoking Main.RemoveHistoryEntries()", "History");
                    main.RemoveHistoryEntries(list);
                }
                catch (Exception ex)
                {
                    Log.Debug($"HB.RequestDelete: exception in Do(): {ex}", "History");
                }
            }

            try
            {
                if (_uiCtx != null) { _uiCtx.Post(_ => Do(), null); return; }
                if (main.InvokeRequired) main.BeginInvoke((Action)Do); else Do();
            }
            catch (Exception ex)
            {
                Log.Debug($"HB.RequestDelete: dispatch failed: {ex}", "History");
            }
        }

    }
}
