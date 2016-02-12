using System;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class EnvironmentInfoMapping {
        public static EnvironmentInfo Map(RaygunModel model) {
            var details = model?.Details;
            if (details == null)
                return null;

            var ei = new EnvironmentInfo { MachineName = details.MachineName };
            var environment = details.Environment;
            if (environment == null)
                return ei;

            ei.Architecture = environment.Architecture;
            ei.AvailablePhysicalMemory = (long)(environment.AvailablePhysicalMemory * 1048576); // Convert MB to bytes
            //ei.CommandLine;
            ei.InstallId = details.User?.Uuid,
            //ei.IpAddress;
            ei.OSName = environment.Platform;
            ei.OSVersion = environment.OsVersion;
            //ei.ProcessId;
            //ei.ProcessMemorySize;
            //ei.ProcessName;
            ei.ProcessorCount = environment.ProcessorCount;
            //ei.RuntimeVersion;
            //ei.ThreadId;
            //ei.ThreadName;
            ei.TotalPhysicalMemory = (long)(environment.TotalPhysicalMemory * 1048576); // Convert MB to bytes

            // Additional Fields
            ei.Data[nameof(environment.AvailableVirtualMemory)] = environment.AvailableVirtualMemory;
            ei.Data[nameof(environment.Browser)] = environment.Browser;
            ei.Data[nameof(environment.BrowserName)] = environment.BrowserName;
            ei.Data[nameof(environment.BrowserVersion)] = environment.BrowserVersion;
            ei.Data[nameof(environment.BrowserHeight)] = environment.BrowserHeight;
            ei.Data[nameof(environment.BrowserWidth)] = environment.BrowserWidth;
            ei.Data[nameof(environment.ColorDepth)] = environment.ColorDepth;
            ei.Data[nameof(environment.Cpu)] = environment.Cpu;
            ei.Data[nameof(environment.CurrentOrientation)] = environment.CurrentOrientation;
            ei.Data[nameof(environment.DeviceManufacturer)] = environment.DeviceManufacturer;
            ei.Data[nameof(environment.DeviceName)] = environment.DeviceName;
            ei.Data[nameof(environment.DiskSpaceFree)] = environment.DiskSpaceFree;
            ei.Data[nameof(environment.Locale)] = environment.Locale;
            ei.Data[nameof(environment.Model)] = environment.Model;
            ei.Data[nameof(environment.PackageVersion)] = environment.PackageVersion;
            ei.Data[nameof(environment.ResolutionScale)] = environment.ResolutionScale;
            ei.Data[nameof(environment.ScreenHeight)] = environment.ScreenHeight;
            ei.Data[nameof(environment.ScreenWidth)] = environment.ScreenWidth;
            ei.Data[nameof(environment.TotalVirtualMemory)] = environment.TotalVirtualMemory;
            ei.Data[nameof(environment.UtcOffset)] = environment.UtcOffset;
            ei.Data[nameof(environment.WindowBoundsHeight)] = environment.WindowBoundsHeight;
            ei.Data[nameof(environment.WindowBoundsWidth)] = environment.WindowBoundsWidth;
            
            return ei;
        }
    }
}
