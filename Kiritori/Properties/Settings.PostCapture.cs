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

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string CapturePostActionSaveFolder
        {
            get { return (string)this[nameof(CapturePostActionSaveFolder)] ?? string.Empty; }
            set { this[nameof(CapturePostActionSaveFolder)] = value; }
        }
    }
}
