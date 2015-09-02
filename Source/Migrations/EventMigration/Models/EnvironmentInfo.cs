using System;
using Exceptionless.Core.Extensions;

namespace Exceptionless.EventMigration.Models {
    public class EnvironmentInfo {
        public EnvironmentInfo() {
            ExtendedData = new ExtendedDataDictionary();
        }

        /// <summary>
        /// Gets the number of processors for the current machine.
        /// </summary>
        /// <value>The number of processors for the current machine.</value>
        public int ProcessorCount { get; set; }

        /// <summary>
        /// Gets the amount of physical memory for the current machine.
        /// </summary>
        /// <value>The amount of physical memory for the current machine.</value>
        public long TotalPhysicalMemory { get; set; }

        /// <summary>
        /// Gets the amount of physical memory mapped to the process context.
        /// </summary>
        /// <value>The amount of physical memory mapped to the process context.</value>
        public long AvailablePhysicalMemory { get; set; }

        /// <summary>
        /// Gets the command line information used to start the process.
        /// </summary>
        /// <value>The command line information used to start the process.</value>
        public string CommandLine { get; set; }

        /// <summary>
        /// The name of the process that the error occurred in.
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Gets the process id.
        /// </summary>
        /// <value>The process id.</value>
        public string ProcessId { get; set; }

        /// <summary>
        /// Gets the amount of physical memory used by the process.
        /// </summary>
        /// <value>The amount of physical memory used by the process.</value>
        public long ProcessMemorySize { get; set; }

        /// <summary>
        /// Gets the name of the thread.
        /// </summary>
        /// <value>The name of the thread.</value>
        public string ThreadName { get; set; }

        /// <summary>
        /// Gets the win32 thread id.
        /// </summary>
        /// <value>The win32 thread id.</value>
        public string ThreadId { get; set; }

        /// <summary>
        /// Gets the OS architecture.
        /// </summary>
        /// <value>The OS architecture.</value>
        public string Architecture { get; set; }

        /// <summary>
        /// The OS name that the error occurred on.
        /// </summary>
        public string OSName { get; set; }

        /// <summary>
        /// The OS version that the error occurred on.
        /// </summary>
        public string OSVersion { get; set; }

        /// <summary>
        /// The name of the machine that the error occurred on.
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        /// The runtime version the application was running under when the error occurred.
        /// </summary>
        public string RuntimeVersion { get; set; }

        /// <summary>
        /// The IP address of the machine the error occurred on.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// Extended data entries for this machine environment.
        /// </summary>
        public ExtendedDataDictionary ExtendedData { get; set; }

        public Exceptionless.Core.Models.Data.EnvironmentInfo ToEnvironmentInfo() {
            var environmentInfo = new Exceptionless.Core.Models.Data.EnvironmentInfo {
                Architecture = Architecture,
                AvailablePhysicalMemory = AvailablePhysicalMemory,
                CommandLine = CommandLine,
                IpAddress = IpAddress,
                MachineName = MachineName,
                OSName = OSName,
                OSVersion = OSVersion,
                ProcessId = ProcessId,
                ProcessMemorySize = ProcessMemorySize,
                ProcessName = ProcessName,
                ProcessorCount = ProcessorCount,
                RuntimeVersion = RuntimeVersion,
                ThreadId = ThreadId,
                ThreadName = ThreadName,
                TotalPhysicalMemory = TotalPhysicalMemory
            };

            if (ExtendedData != null && ExtendedData.Count > 0)
                environmentInfo.Data.AddRange(ExtendedData.ToData());

            return environmentInfo;
        }
    }
}