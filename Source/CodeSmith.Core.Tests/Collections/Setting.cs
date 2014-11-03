using System;
using System.Configuration;
using System.Diagnostics;

namespace CodeSmith.Core.Tests.Collections
{
    public class Setting : ApplicationSettingsBase
    {
        private static readonly Setting _defaultInstance = ((Setting) (Synchronized(new Setting())));

        public static Setting Default
        {
            get { return _defaultInstance; }
        }

        [UserScopedSetting]
        public DateTime InstallDate
        {
            get { return ((DateTime) (this["InstallDate"])); }
            set { this["InstallDate"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("00000000-0000-0000-0000-000000000000")]
        public Guid InstallIdentifier
        {
            get { return ((Guid) (this["InstallIdentifier"])); }
            set { this["InstallIdentifier"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int RunCount
        {
            get { return ((int) (this["RunCount"])); }
            set { this["RunCount"] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int SubmitCount
        {
            get { return ((int) (this["SubmitCount"])); }
            set { this["SubmitCount"] = value; }
        }

    }
}