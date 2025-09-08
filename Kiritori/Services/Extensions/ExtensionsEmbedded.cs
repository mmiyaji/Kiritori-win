using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization.Json;

namespace Kiritori.Services.Extensions
{
    internal static class ExtensionsEmbedded
    {
        public static List<ExtensionManifest> LoadEmbedded()
        {
            var list = new List<ExtensionManifest>();
            var asm = Assembly.GetExecutingAssembly();
            var names = asm.GetManifestResourceNames();

            foreach (var res in names)
            {
                if (!res.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                // ThirdPartyManifests または EmbeddedManifests のどちらでもOK
                if (!(res.IndexOf(".ThirdPartyManifests.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    res.IndexOf(".EmbeddedManifests.", StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                try
                {
                    using (var s = asm.GetManifestResourceStream(res))
                    {
                        if (s == null) continue;
                        var ser = new DataContractJsonSerializer(typeof(ExtensionManifest));
                        var m = (ExtensionManifest)ser.ReadObject(s);
                        if (m != null) list.Add(m);
                    }
                }
                catch { /* 破損はスキップ */ }
            }
            return list;
        }
    }
}
