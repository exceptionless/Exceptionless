using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class EnvironmentInfoMapping {
        public static EnvironmentInfo Map(RaygunModel raygunModel) {
            var raygunEnvironment = raygunModel?.Details?.Environment;

            if (raygunEnvironment == null) {
                return null;
            }

            var environmentInfo = new EnvironmentInfo();

            environmentInfo.Architecture = raygunEnvironment.Architecture;
            environmentInfo.AvailablePhysicalMemory = Convert.ToInt64(raygunEnvironment.AvailablePhysicalMemory);
            environmentInfo.TotalPhysicalMemory = Convert.ToInt64(raygunEnvironment.TotalPhysicalMemory);
            environmentInfo.MachineName = raygunEnvironment.DeviceName;
            environmentInfo.OSVersion = raygunEnvironment.OsVersion;

            return environmentInfo;
        }
    }
}
