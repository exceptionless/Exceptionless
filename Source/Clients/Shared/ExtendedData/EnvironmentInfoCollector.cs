#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Exceptionless.Models;
using Microsoft.VisualBasic.Devices;

namespace Exceptionless.ExtendedData {
    internal static class EnvironmentInfoCollector {
        private static EnvironmentInfo _environmentInfo;

        /// <summary>
        /// Collect information about the current machine.
        /// </summary>
        public static EnvironmentInfo Collect(bool forceRefresh = false) {
            if (_environmentInfo != null && !forceRefresh)
                return _environmentInfo;

            var machineInfo = new EnvironmentInfo();
            ComputerInfo computerInfo = null;

            try {
                computerInfo = new ComputerInfo();
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get computer info.  Error message: {0}", e.Message));
            }

            try {
#if SILVERLIGHT
                _operatingSystem = Environment.OSVersion.Version.ToString();
                _operatingSystemVersion = Environment.OSVersion.Version;
#else
                if (computerInfo != null)
                    machineInfo.OSName = computerInfo.OSFullName;
                if (computerInfo != null)
                    machineInfo.OSVersion = computerInfo.OSVersion;
#endif
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get operating system version. Error message: {0}", e.Message));
            }

            try {
                if (computerInfo != null)
                    machineInfo.TotalPhysicalMemory = Convert.ToInt64(computerInfo.TotalPhysicalMemory);
                if (computerInfo != null)
                    machineInfo.AvailablePhysicalMemory = Convert.ToInt64(computerInfo.AvailablePhysicalMemory);
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get physical memory. Error message: {0}", e.Message));
            }

            try {
                machineInfo.ProcessorCount = Environment.ProcessorCount;
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get processor count. Error message: {0}", e.Message));
            }

#if !SILVERLIGHT
            try {
                machineInfo.MachineName = Environment.MachineName;
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get machine name. Error message: {0}", e.Message));
            }

            try {
                IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ip = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                if (ip != null)
                    machineInfo.IpAddress = ip.ToString();
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get ip address. Error message: {0}", e.Message));
            }

            try {
                Process proc = Process.GetCurrentProcess();
                machineInfo.ProcessMemorySize = proc.PrivateMemorySize64;
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to process memory size. Error message: {0}", e.Message));
            }

            try {
                machineInfo.CommandLine = Environment.CommandLine;
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get command line. Error message: {0}", e.Message));
            }

            try {
                machineInfo.ProcessId = KernelNativeMethods.GetCurrentProcessId().ToString(NumberFormatInfo.InvariantInfo);
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get process id. Error message: {0}", e.Message));
            }

            try {
                machineInfo.ProcessName = GetProcessName();
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get process name. Error message: {0}", e.Message));
            }

            try {
                machineInfo.ThreadId = KernelNativeMethods.GetCurrentThreadId().ToString(NumberFormatInfo.InvariantInfo);
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get thread id. Error message: {0}", e.Message));
            }

            try {
                machineInfo.Architecture = Is64BitOperatingSystem() ? "x64" : "x86";
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get CPU architecture. Error message: {0}", e.Message));
            }
#endif
            try {
                machineInfo.RuntimeVersion = Environment.Version.ToString();
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get CLR version. Error message: {0}", e.Message));
            }

            try {
                machineInfo.ExtendedData.Add("AppDomainName", AppDomain.CurrentDomain.FriendlyName);
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get current AppDomain friendly name. Error message: {0}", e.Message));
            }

            try {
                machineInfo.ThreadName = Thread.CurrentThread.Name;
            } catch (Exception e) {
                Trace.WriteLine(String.Format("Unable to get current thread name. Error message: {0}", e.Message));
            }

            _environmentInfo = machineInfo;
            return _environmentInfo;
        }

#if !SILVERLIGHT
        private static string GetProcessName() {
            var buffer = new StringBuilder(1024);
            int length = KernelNativeMethods.GetModuleFileName(KernelNativeMethods.GetModuleHandle(null), buffer, buffer.Capacity);
            return buffer.ToString();
        }

        private static bool Is64BitOperatingSystem() {
            if (IntPtr.Size == 8) // 64-bit programs run only on Win64
                return true;

            // Detect whether the current process is a 32-bit process running on a 64-bit system.
            bool is64;
            bool methodExist = KernelNativeMethods.MethodExists("kernel32.dll", "IsWow64Process");

            return ((methodExist && KernelNativeMethods.IsWow64Process(KernelNativeMethods.GetCurrentProcess(), out is64)) && is64);
        }
#endif
    }

    internal static class KernelNativeMethods {
        #region Kernel32

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        public static extern int GetCurrentProcessId();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [PreserveSig]
        public static extern int GetModuleFileName([In] IntPtr hModule, [Out] StringBuilder lpFilename, [In] [MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll")]
        public static extern int GetCurrentThreadId();

        #endregion

        public static bool MethodExists(string moduleName, string methodName) {
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            if (moduleHandle == IntPtr.Zero)
                return false;

            return (GetProcAddress(moduleHandle, methodName) != IntPtr.Zero);
        }
    }
}