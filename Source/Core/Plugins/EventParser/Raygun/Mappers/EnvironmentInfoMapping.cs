using System;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class EnvironmentInfoMapping {
        // TODO: Node memory sizes are in bytes while .NET's are in MB.. We need to normalize this..
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
            ei.InstallId = details.User?.Uuid;
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
            if (environment.AvailableVirtualMemory > 0)
                ei.Data[nameof(environment.AvailableVirtualMemory)] = environment.AvailableVirtualMemory * 1048576; // Convert MB to bytes
            if (!String.IsNullOrEmpty(environment.Browser))
                ei.Data[nameof(environment.Browser)] = environment.Browser;
            if (!String.IsNullOrEmpty(environment.BrowserName))
                ei.Data[nameof(environment.BrowserName)] = environment.BrowserName;
            if (!String.IsNullOrEmpty(environment.BrowserVersion))
                ei.Data[nameof(environment.BrowserVersion)] = environment.BrowserVersion;
            if (environment.BrowserHeight > 0)
                ei.Data[nameof(environment.BrowserHeight)] = environment.BrowserHeight;
            if (environment.BrowserWidth > 0)
                ei.Data[nameof(environment.BrowserWidth)] = environment.BrowserWidth;
            if (environment.ColorDepth > 0)
                ei.Data[nameof(environment.ColorDepth)] = environment.ColorDepth;
            if (!String.IsNullOrEmpty(environment.Cpu))
                ei.Data[nameof(environment.Cpu)] = environment.Cpu;
            if (!String.IsNullOrEmpty(environment.CurrentOrientation))
                ei.Data[nameof(environment.CurrentOrientation)] = environment.CurrentOrientation;
            if (!String.IsNullOrEmpty(environment.DeviceManufacturer))
                ei.Data[nameof(environment.DeviceManufacturer)] = environment.DeviceManufacturer;
            if (!String.IsNullOrEmpty(environment.DeviceName))
                ei.Data[nameof(environment.DeviceName)] = environment.DeviceName;
            if (environment.DiskSpaceFree?.Count > 0)
                ei.Data[nameof(environment.DiskSpaceFree)] = environment.DiskSpaceFree;
            if (!String.IsNullOrEmpty(environment.Locale))
                ei.Data[nameof(environment.Locale)] = environment.Locale;
            if (!String.IsNullOrEmpty(environment.Model))
                ei.Data[nameof(environment.Model)] = environment.Model;
            if (!String.IsNullOrEmpty(environment.PackageVersion))
                ei.Data[nameof(environment.PackageVersion)] = environment.PackageVersion;
            if (environment.ResolutionScale > 0)
                ei.Data[nameof(environment.ResolutionScale)] = environment.ResolutionScale;
            if (environment.ScreenHeight > 0)
                ei.Data[nameof(environment.ScreenHeight)] = environment.ScreenHeight;
            if (environment.ScreenWidth > 0)
                ei.Data[nameof(environment.ScreenWidth)] = environment.ScreenWidth;
            if (environment.TotalVirtualMemory > 0)
                ei.Data[nameof(environment.TotalVirtualMemory)] = environment.TotalVirtualMemory * 1048576; // Convert MB to bytes
            if (environment.UtcOffset > 0)
                ei.Data[nameof(environment.UtcOffset)] = environment.UtcOffset;
            if (environment.WindowBoundsHeight > 0)
                ei.Data[nameof(environment.WindowBoundsHeight)] = environment.WindowBoundsHeight;
            if (environment.WindowBoundsWidth > 0)
                ei.Data[nameof(environment.WindowBoundsWidth)] = environment.WindowBoundsWidth;
            
            return ei;
        }
    }
}
