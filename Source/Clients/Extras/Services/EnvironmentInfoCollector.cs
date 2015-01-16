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
using Exceptionless.Logging;
using Exceptionless.Models.Data;
using Microsoft.VisualBasic.Devices;

namespace Exceptionless.Services {
    public class EnvironmentInfoCollector : IEnvironmentInfoCollector {
        private static EnvironmentInfo _environmentInfo;
        private readonly IExceptionlessLog _log;

        public EnvironmentInfoCollector(IExceptionlessLog log) {
            _log = log;
        }

        public EnvironmentInfo GetEnvironmentInfo() {
            if (_environmentInfo != null)
                return _environmentInfo;

            var info = new EnvironmentInfo();
            ComputerInfo computerInfo = null;

            try {
                computerInfo = new ComputerInfo();
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get computer info. Error message: {0}", ex.Message);
            }

            try {
                if (computerInfo != null)
                    info.OSName = computerInfo.OSFullName;
                if (computerInfo != null)
                    info.OSVersion = computerInfo.OSVersion;
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get operating system version. Error message: {0}", ex.Message);
            }

            try {
                if (computerInfo != null)
                    info.TotalPhysicalMemory = Convert.ToInt64(computerInfo.TotalPhysicalMemory);
                if (computerInfo != null)
                    info.AvailablePhysicalMemory = Convert.ToInt64(computerInfo.AvailablePhysicalMemory);
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get physical memory. Error message: {0}", ex.Message);
            }

            try {
                info.ProcessorCount = Environment.ProcessorCount;
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get processor count. Error message: {0}", ex.Message);
            }

            try {
                info.MachineName = Environment.MachineName;
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get machine name. Error message: {0}", ex.Message);
            }

            try {
                IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                if (hostEntry != null && hostEntry.AddressList.Any())
                    info.IpAddress = String.Join(", ", hostEntry.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()).ToArray());
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get ip address. Error message: {0}", ex.Message);
            }

            try {
                Process proc = Process.GetCurrentProcess();
                info.ProcessMemorySize = proc.PrivateMemorySize64;
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get process memory size. Error message: {0}", ex.Message);
            }

            try {
                info.CommandLine = Environment.CommandLine;
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get command line. Error message: {0}", ex.Message);
            }

            try {
                info.ProcessId = KernelNativeMethods.GetCurrentProcessId().ToString(NumberFormatInfo.InvariantInfo);
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get process id. Error message: {0}", ex.Message);
            }

            try {
                info.ProcessName = GetProcessName();
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get process name. Error message: {0}", ex.Message);
            }

            try {
                info.ThreadId = KernelNativeMethods.GetCurrentThreadId().ToString(NumberFormatInfo.InvariantInfo);
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get thread id. Error message: {0}", ex.Message);
            }

            try {
                info.Architecture = Is64BitOperatingSystem() ? "x64" : "x86";
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get CPU architecture. Error message: {0}", ex.Message);
            }

            try {
                info.RuntimeVersion = Environment.Version.ToString();
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get CLR version. Error message: {0}", ex.Message);
            }

            try {
                info.Data.Add("AppDomainName", AppDomain.CurrentDomain.FriendlyName);
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get AppDomain friendly name. Error message: {0}", ex.Message);
            }

            try {
                info.ThreadName = Thread.CurrentThread.Name;
            } catch (Exception ex) {
                _log.FormattedInfo(typeof(EnvironmentInfoCollector), "Unable to get current thread name. Error message: {0}", ex.Message);
            }

            _environmentInfo = info;
            return _environmentInfo;
        }

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