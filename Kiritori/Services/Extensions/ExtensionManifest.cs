using System.Runtime.Serialization;

namespace Kiritori.Services.Extensions
{
    [DataContract]
    internal sealed class ExtensionManifest
    {
        [DataMember(Name = "id")]           public string Id            { get; set; }
        [DataMember(Name = "displayName")]  public string DisplayName   { get; set; }
        [DataMember(Name = "version")]      public string Version       { get; set; }
        [DataMember(Name = "kind")]         public string Kind          { get; set; } // "assembly" | "satellite" | "tool"
        [DataMember(Name = "download")]     public DownloadInfo Download { get; set; }
        [DataMember(Name = "install")]      public InstallInfo  Install  { get; set; }
        [DataMember(Name = "requires")]     public RequiresInfo Requires { get; set; }
        [DataMember(Name = "description")]  public string Description   { get; set; } // 任意

        [DataContract]
        internal sealed class DownloadInfo
        {
            [DataMember(Name = "url")]    public string Url    { get; set; }
            [DataMember(Name = "sha256")] public string Sha256 { get; set; }
            [DataMember(Name = "size")]   public long Size   { get; set; }
        }

        [DataContract]
        internal sealed class InstallInfo
        {
            [DataMember(Name = "targetDir")] public string   TargetDir { get; set; }
            [DataMember(Name = "files")]     public string[] Files     { get; set; }
        }

        [DataContract]
        internal sealed class RequiresInfo
        {
            [DataMember(Name = "osMin")] public string OsMin { get; set; }
        }
    }
}
