// Services/History/HistoryBridge.cs など
using System;
using System.Collections.Generic;
using System.Linq;
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

        // ★ここを追加：外部から発火できるようにする
        public static void RaiseChanged(object sender = null)
        {
            var h = HistoryChanged;
            if (h != null) h(sender ?? typeof(HistoryBridge), EventArgs.Empty);
        }

        public static void TryBindFromOpenForms()
        {
            if (_provider != null) return;
            var main = Application.OpenForms.OfType<MainApplication>().FirstOrDefault();
            if (main != null) SetProvider(() => main.GetHistoryEntriesSnapshot());
        }
    }
}
