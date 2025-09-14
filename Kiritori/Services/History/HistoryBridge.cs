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

        public static void TryBindFromOpenForms()
        {
            if (_provider != null) return;
            var main = Application.OpenForms.OfType<MainApplication>().FirstOrDefault();
            if (main != null) SetProvider(() => main.GetHistoryEntriesSnapshot());
        }
        public static void RaiseChanged(object sender = null)
        {
            var h = HistoryChanged;
            if (h != null) h(sender ?? typeof(HistoryBridge), EventArgs.Empty);
        }


        // PrefForm など UI 以外から、安全に「削除して」と頼む用。
        // MainApplication が開いていればそこへフォワードします。
        public static void RequestDelete(IEnumerable<HistoryEntry> targets)
        {
            if (targets == null) return;
            var main = Application.OpenForms.OfType<MainApplication>().FirstOrDefault();
            if (main != null && main.IsHandleCreated)
            {
                // UI スレッドで実行
                main.BeginInvoke((Action)(() => main.RemoveHistoryEntries(targets)));
            }
        }


    }
}
