#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models.Data {
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
    }
}