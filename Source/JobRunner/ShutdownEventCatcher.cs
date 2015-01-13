using System;
using System.Runtime.InteropServices;

namespace Exceptionless.JobRunner {
    public static class ShutdownEventCatcher {
        public static event Action<ShutdownEventArgs> Shutdown;
        static void RaiseShutdownEvent(ShutdownEventArgs args) {
            if (null != Shutdown)
                Shutdown(args);
        }

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(Kernel32ShutdownHandler handler, bool add);

        private delegate bool Kernel32ShutdownHandler(ShutdownReason reason);

        static ShutdownEventCatcher() {
            SetConsoleCtrlHandler(new Kernel32ShutdownHandler(Kernel32_ProcessShuttingDown), true);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
            var args = new ShutdownEventArgs(ShutdownReason.ReachEndOfMain);
            RaiseShutdownEvent(args);
        }
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            var args = new ShutdownEventArgs(e.ExceptionObject as Exception);
            RaiseShutdownEvent(args);
        }
        static bool Kernel32_ProcessShuttingDown(ShutdownReason sig) {
            ShutdownEventArgs args = new ShutdownEventArgs(sig);
            RaiseShutdownEvent(args);
            return false;
        }
    }

    public enum ShutdownReason {
        PressCtrlC = 0,
        PressCtrlBreak = 1,
        ConsoleClosing = 2,
        WindowsLogOff = 5,
        WindowsShutdown = 6,
        ReachEndOfMain = 1000,
        Exception = 1001
    }

    public class ShutdownEventArgs {
        public readonly Exception Exception;
        public readonly ShutdownReason Reason;

        public ShutdownEventArgs(ShutdownReason reason) {
            Reason = reason;
            Exception = null;
        }

        public ShutdownEventArgs(Exception exception) {
            Reason = ShutdownReason.Exception;
            Exception = exception;
        }
    }
}