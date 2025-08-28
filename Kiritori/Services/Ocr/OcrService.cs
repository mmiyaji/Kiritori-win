using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Automation.Provider;

namespace Kiritori.Services.Ocr
{
    public sealed class OcrService
    {
        private readonly List<IOcrProvider> _providers = new List<IOcrProvider>();

        public OcrService()
        {
            _providers.Add(new WindowsOcrProvider());
            //if (TesseractOcrProvider.CanLoad())
            //    _providers.Add(new TesseractOcrProvider());
        }

        public IEnumerable<IOcrProvider> Providers { get { return _providers; } }

        public IOcrProvider Get(string preferredName)
        {
            if (!string.IsNullOrEmpty(preferredName))
            {
                var hit = _providers.FirstOrDefault(p => p.Name == preferredName);
                if (hit != null) return hit;
            }
            return _providers.First();
        }
    }
}
