using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Environment {
        [JsonProperty("processorCount")]
        public int ProcessorCount { get; set; }

        [JsonProperty("osVersion")]
        public string OsVersion { get; set; }

        [JsonProperty("windowBoundsWidth")]
        public double WindowBoundsWidth { get; set; }

        [JsonProperty("windowBoundsHeight")]
        public double WindowBoundsHeight { get; set; }

        //[JsonProperty("browser-Width")]
        //public int BrowserWidth { get; set; }

        //[JsonProperty("browser-Height")]
        //public int BrowserHeight { get; set; }

        //[JsonProperty("screen-Width")]
        //public int ScreenWidth { get; set; }

        //[JsonProperty("screen-Height")]
        //public int ScreenHeight { get; set; }

        //[JsonProperty("resolutionScale")]
        //public int ResolutionScale { get; set; }

        //[JsonProperty("color-Depth")]
        //public int ColorDepth { get; set; }

        //[JsonProperty("currentOrientation")]
        //public string CurrentOrientation { get; set; }

        [JsonProperty("cpu")]
        public string Cpu { get; set; }

        //[JsonProperty("packageVersion")]
        //public string PackageVersion { get; set; }

        [JsonProperty("architecture")]
        public string Architecture { get; set; }

        //[JsonProperty("deviceManufacturer")]
        //public string DeviceManufacturer { get; set; }

        //[JsonProperty("model")]
        //public string Model { get; set; }

        [JsonProperty("totalPhysicalMemory")]
        public ulong TotalPhysicalMemory { get; set; }

        [JsonProperty("availablePhysicalMemory")]
        public ulong AvailablePhysicalMemory { get; set; }

        [JsonProperty("totalVirtualMemory")]
        public ulong TotalVirtualMemory { get; set; }

        [JsonProperty("availableVirtualMemory")]
        public ulong AvailableVirtualMemory { get; set; }

        [JsonProperty("diskSpaceFree")]
        public IList<double> DiskSpaceFree { get; set; }

        [JsonProperty("deviceName")]
        public string DeviceName { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("utcOffset")]
        public double UtcOffset { get; set; }

        //[JsonProperty("browser")]
        //public string Browser { get; set; }

        //[JsonProperty("browserName")]
        //public string BrowserName { get; set; }

        //[JsonProperty("browser-Version")]
        //public string BrowserVersion { get; set; }

        //[JsonProperty("platform")]
        //public string Platform { get; set; }
    }
}
