using Kiritori.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Kiritori.Services.Extensions
{
    internal sealed class ExtensionState
    {
        public Dictionary<string, Item> Items = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        internal sealed class Item
        {
            public bool Installed;
            public bool Enabled = true;
            public string Version;
        }

        public static ExtensionState Load()
        {
            try
            {
                var path = ExtensionsPaths.StateJson;
                if (File.Exists(path))
                {
                    using (var fs = File.OpenRead(path))
                    {
                        var ser = new DataContractJsonSerializer(typeof(ExtensionState));
                        return (ExtensionState)ser.ReadObject(fs);
                    }
                }
            }
            catch { }
            return new ExtensionState();
        }

        public void Save()
        {
            try
            {
                ExtensionsPaths.EnsureDirs();
                var path = ExtensionsPaths.StateJson;
                using (var ms = new MemoryStream())
                {
                    var ser = new DataContractJsonSerializer(typeof(ExtensionState));
                    ser.WriteObject(ms, this);
                    File.WriteAllText(path, Encoding.UTF8.GetString(ms.ToArray()), Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
