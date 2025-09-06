using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Kiritori.Helpers
{
    /// <summary>
    /// [LocalizedDescription("ResourceKey", typeof(Properties.SettingsDescriptions))]
    /// のように使う。Description プロパティは現在の CurrentUICulture で解決される。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private readonly string _resourceKey;
        private readonly ResourceManager _rm;

        public LocalizedDescriptionAttribute(string resourceKey, Type resourceType)
            : base(resourceKey)
        {
            _resourceKey = resourceKey ?? "";
            _rm = (ResourceManager)resourceType
                .GetProperty("ResourceManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static)
                ?.GetValue(null, null);
        }

        public override string Description
        {
            get
            {
                if (_rm == null || string.IsNullOrEmpty(_resourceKey))
                    return base.Description;

                var s = _rm.GetString(_resourceKey, CultureInfo.CurrentUICulture);
                return string.IsNullOrEmpty(s) ? base.Description : s;
            }
        }
    }
}
