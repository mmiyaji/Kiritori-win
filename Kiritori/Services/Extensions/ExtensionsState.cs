using Kiritori.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Kiritori.Services.Extensions
{
    [DataContract]
    internal sealed class ExtensionState
    {
        [DataMember]
        public Dictionary<string, Item> Items { get; set; }
            = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        [DataContract]
        internal sealed class Item
        {
            [DataMember] public bool Installed;
            [DataMember] public bool Enabled = true;
            [DataMember] public string Version;
        }

        private static DataContractJsonSerializer CreateSerializer() =>
            new DataContractJsonSerializer(typeof(ExtensionState),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        public static ExtensionState Load()
        {
            try
            {
                var path = ExtensionsPaths.StateJson;
                if (File.Exists(path))
                {
                    using (var fs = File.OpenRead(path))
                    {
                        var ser = CreateSerializer();
                        return (ExtensionState)ser.ReadObject(fs);
                    }
                }
            }
            catch { /* 初回などは新規生成 */ }
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
                    var ser = CreateSerializer();
                    ser.WriteObject(ms, this);
                    File.WriteAllText(path, Encoding.UTF8.GetString(ms.ToArray()), Encoding.UTF8);
                }
            }
            catch { /* 書き込み失敗は握りつぶし（必要ならログ） */ }
        }
    }
}