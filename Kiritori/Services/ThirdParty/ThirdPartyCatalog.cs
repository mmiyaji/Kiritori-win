using Kiritori.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Reflection;

namespace Kiritori.Services.ThirdParty
{
[DataContract]
    internal sealed class ThirdPartyComponent
    {
        [DataMember(Name = "id")]           public string Id          { get; set; } = string.Empty;
        [DataMember(Name = "name")]         public string Name        { get; set; } = string.Empty;
        [DataMember(Name = "version")]      public string Version     { get; set; } = string.Empty;
        [DataMember(Name = "license")]      public string License     { get; set; } = string.Empty;
        [DataMember(Name = "projectUrl")]   public string ProjectUrl  { get; set; } = string.Empty;
        [DataMember(Name = "licenseUrl")]   public string LicenseUrl  { get; set; } = string.Empty;

        // 旧: 外部ファイル参照（後方互換）
        [DataMember(Name = "licenseFile")]  public string LicenseFile { get; set; } = string.Empty;
        [DataMember(Name = "noticeFile")]   public string NoticeFile  { get; set; } = string.Empty;

        // 新: 埋め込みリソース参照
        [DataMember(Name = "licenseRes")]   public string LicenseRes  { get; set; } = string.Empty;
        [DataMember(Name = "noticeRes")]    public string NoticeRes   { get; set; } = string.Empty;

        // 読み込み後にセット
        internal string LicenseText { get; set; } = string.Empty;
        internal string NoticeText  { get; set; } = string.Empty;
    }

    [DataContract]
    internal sealed class ThirdPartyCatalogDto
    {
        [DataMember(Name = "components")]
        public List<ThirdPartyComponent> Components { get; set; } = new List<ThirdPartyComponent>();
    }

    internal static class ThirdPartyCatalog
    {
        /// <summary>
        /// Assembly 埋め込みリソースから thirdparty.json を探してロード
        /// </summary>
        public static List<ThirdPartyComponent> LoadFromEmbedded()
        {
            var list = new List<ThirdPartyComponent>();
            try
            {
                var asm = typeof(ThirdPartyCatalog).Assembly;
                var resNames = asm.GetManifestResourceNames();

                // 末尾一致で thirdparty.json を発見（例: Kiritori.ThirdParty.thirdparty.json）
                var jsonRes = resNames.FirstOrDefault(n =>
                    n.EndsWith(".ThirdParty.thirdparty.json", StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith(".thirdparty.json", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(jsonRes))
                {
                    Log.Debug("thirdparty.json not found in embedded resources", "ThirdParty");
                    return list;
                }

                var json = ReadEmbeddedText(asm, jsonRes);
                var dto = Deserialize<ThirdPartyCatalogDto>(json);
                if (dto == null || dto.Components == null) return list;

                foreach (var c in dto.Components)
                {
                    // 優先: licenseRes / noticeRes（埋め込み）
                    if (!string.IsNullOrEmpty(c.LicenseRes))
                        c.LicenseText = TryReadBySuffix(asm, resNames, c.LicenseRes);

                    if (!string.IsNullOrEmpty(c.NoticeRes))
                        c.NoticeText = TryReadBySuffix(asm, resNames, c.NoticeRes);

                    // 後方互換: licenseFile / noticeFile → パス文字列をそのまま末尾照合に使う
                    if (string.IsNullOrEmpty(c.LicenseText) && !string.IsNullOrEmpty(c.LicenseFile))
                        c.LicenseText = TryReadBySuffix(asm, resNames, c.LicenseFile);

                    if (string.IsNullOrEmpty(c.NoticeText) && !string.IsNullOrEmpty(c.NoticeFile))
                        c.NoticeText = TryReadBySuffix(asm, resNames, c.NoticeFile);

                    list.Add(c);
                }
            }
            catch
            {
                Log.Debug("LoadFromEmbedded failed", "ThirdParty");
            }
            return list;
        }

        private static string TryReadBySuffix(Assembly asm, string[] resNames, string suffixLikePath)
        {
            if (string.IsNullOrEmpty(suffixLikePath)) return string.Empty;

            // パス風 → 埋め込み名空間のドット区切り末尾に変換イメージで比較
            var norm = suffixLikePath
                .Replace('\\', '/')
                .TrimStart('/')
                .Replace('/', '.');

            // 末尾一致で検索（大文字小文字無視）
            var hit = resNames.FirstOrDefault(n =>
                n.EndsWith("." + norm, StringComparison.OrdinalIgnoreCase) ||
                n.EndsWith(norm, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(hit)) return string.Empty;
            return ReadEmbeddedText(asm, hit);
        }

        private static string ReadEmbeddedText(Assembly asm, string resName)
        {
            try
            {
                using (var s = asm.GetManifestResourceStream(resName))
                {
                    if (s == null) return string.Empty;
                    using (var r = new StreamReader(s, new UTF8Encoding(false)))
                        return r.ReadToEnd();
                }
            }
            catch { return string.Empty; }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var ser = new DataContractJsonSerializer(typeof(T));
                    return ser.ReadObject(ms) as T;
                }
            }
            catch { return null; }
        }
    }
}
