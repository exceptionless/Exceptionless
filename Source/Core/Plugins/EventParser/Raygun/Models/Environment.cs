using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Environment {
        public int ProcessorCount { get; set; }

        public string OsVersion { get; set; }

        public double WindowBoundsWidth { get; set; }

        public double WindowBoundsHeight { get; set; }

        public int BrowserWidth { get; set; }

        public int BrowserHeight { get; set; }

        public int ScreenWidth { get; set; }

        public int ScreenHeight { get; set; }

        public double ResolutionScale { get; set; }

        public int ColorDepth { get; set; }

        public string CurrentOrientation { get; set; }

        public string Cpu { get; set; }

        public string PackageVersion { get; set; }

        public string Architecture { get; set; }

        public string DeviceManufacturer { get; set; }

        public string Model { get; set; }

        public ulong TotalPhysicalMemory { get; set; }

        public ulong AvailablePhysicalMemory { get; set; }

        public ulong TotalVirtualMemory { get; set; }

        public ulong AvailableVirtualMemory { get; set; }

        public IList<double> DiskSpaceFree { get; set; }

        public string DeviceName { get; set; }

        public string Locale { get; set; }

        public double UtcOffset { get; set; }

        public string Browser { get; set; }

        public string BrowserName { get; set; }

        public string BrowserVersion { get; set; }

        public string Platform { get; set; }
    }
}
