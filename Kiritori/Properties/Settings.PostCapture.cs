using System.Configuration;

namespace Kiritori.Properties
{
    internal sealed partial class Settings
    {
        [UserScopedSetting]
        [DefaultSettingValue("None")]
        public string CapturePostActionPreset
        {
            get { return (string)this[nameof(CapturePostActionPreset)] ?? "None"; }
            set { this[nameof(CapturePostActionPreset)] = value; }
        }
    }
}
