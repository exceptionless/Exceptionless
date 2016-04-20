using System;

namespace Exceptionless.Core.Models.Data {
    public class EnvironmentInfo : IData {
        public EnvironmentInfo() {
            Data = new DataDictionary();
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
        /// The Ip Address of the machine that the error occurred on.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// The name of the machine that the error occurred on.
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        /// A unique value identifying each Exceptionless client installation.
        /// </summary>
        public string InstallId { get; set; }

        /// <summary>
        /// The runtime version the application was running under when the error occurred.
        /// </summary>
        public string RuntimeVersion { get; set; }

        /// <summary>
        /// Extended data entries for this machine environment.
        /// </summary>
        public DataDictionary Data { get; set; }

        protected bool Equals(EnvironmentInfo other) {
            return ProcessorCount == other.ProcessorCount && TotalPhysicalMemory == other.TotalPhysicalMemory && AvailablePhysicalMemory == other.AvailablePhysicalMemory && string.Equals(CommandLine, other.CommandLine) && string.Equals(ProcessName, other.ProcessName) && string.Equals(ProcessId, other.ProcessId) && ProcessMemorySize == other.ProcessMemorySize && string.Equals(ThreadName, other.ThreadName) && string.Equals(ThreadId, other.ThreadId) && string.Equals(Architecture, other.Architecture) && string.Equals(OSName, other.OSName) && string.Equals(OSVersion, other.OSVersion) && string.Equals(IpAddress, other.IpAddress) && string.Equals(MachineName, other.MachineName) && string.Equals(InstallId, other.InstallId) && string.Equals(RuntimeVersion, other.RuntimeVersion) && Equals(Data, other.Data);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((EnvironmentInfo)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = ProcessorCount;
                hashCode = (hashCode * 397) ^ TotalPhysicalMemory.GetHashCode();
                hashCode = (hashCode * 397) ^ (CommandLine == null ? 0 : CommandLine.GetHashCode());
                hashCode = (hashCode * 397) ^ (ProcessName == null ? 0 : ProcessName.GetHashCode());
                hashCode = (hashCode * 397) ^ (ProcessId == null ? 0 : ProcessId.GetHashCode());
                hashCode = (hashCode * 397) ^ (Architecture == null ? 0 : Architecture.GetHashCode());
                hashCode = (hashCode * 397) ^ (OSName == null ? 0 : OSName.GetHashCode());
                hashCode = (hashCode * 397) ^ (OSVersion == null ? 0 :OSVersion.GetHashCode());
                hashCode = (hashCode * 397) ^ (IpAddress == null ? 0 : IpAddress.GetHashCode());
                hashCode = (hashCode * 397) ^ (MachineName == null ? 0 : MachineName.GetHashCode());
                hashCode = (hashCode * 397) ^ (InstallId == null ? 0 : InstallId.GetHashCode());
                hashCode = (hashCode * 397) ^ (RuntimeVersion == null ? 0 : RuntimeVersion.GetHashCode());
                hashCode = (hashCode * 397) ^ (Data == null ? 0 : Data.GetCollectionHashCode());
                return hashCode;
            }
        }
    }
}