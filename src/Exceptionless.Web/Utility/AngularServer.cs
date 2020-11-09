using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SpaServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Exceptionless.Web.Utility {
    public static class Connection {
        private static int Port { get; } = 5100;
        private static Uri DevelopmentServerEndpoint { get; } = new Uri($"http://localhost:{Port}");
        private static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(60);

        private static string DoneMessage { get; } = "Waiting...";

        public static void UseAngularDevelopmentServer(this ISpaBuilder spa) {
            spa.UseProxyToSpaDevelopmentServer(async () => {
                var loggerFactory = spa.ApplicationBuilder.ApplicationServices.GetService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Vue");

                if (IsRunning())
                    return DevelopmentServerEndpoint;

                string version = "0.0.0";
                try {
                    var versionInfo = FileVersionInfo.GetVersionInfo(typeof(Connection).Assembly.Location);
                    version = versionInfo.ProductVersion;
                    var parts = version.Split('+');
                    if (parts.Length == 2)
                        version = parts[0];
                }
                catch { }

                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var processInfo = new ProcessStartInfo {
                    FileName = isWindows ? "cmd" : "npm",
                    Arguments = $"{(isWindows ? "/c npm " : "")}run serve",
                    WorkingDirectory = "ClientApp",
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    EnvironmentVariables = { { "UI_VERSION", version } }
                };

                var process = Process.Start(processInfo);

                var tcs = new TaskCompletionSource<int>();
                _ = Task.Run(() => {
                    try {
                        string line;
                        while ((line = process.StandardOutput.ReadLine()) != null) {
                            logger.LogInformation(line);
                            if (!tcs.Task.IsCompleted && line.Contains(DoneMessage))
                                tcs.SetResult(1);
                        }
                    } catch (EndOfStreamException ex) {
                        logger.LogError(ex.ToString());
                        tcs.SetException(new InvalidOperationException("'npm run serve' failed.", ex));
                    }
                });

                _ = Task.Run(() => {
                    try {
                        string line;
                        while ((line = process.StandardError.ReadLine()) != null) {
                            logger.LogError(line);
                        }
                    } catch (EndOfStreamException ex) {
                        logger.LogError(ex.ToString());
                        tcs.SetException(new InvalidOperationException("'npm run serve' failed.", ex));
                    }
                });

                var timeout = Task.Delay(Timeout);
                if (await Task.WhenAny(timeout, tcs.Task) == timeout)
                    throw new TimeoutException();

                return DevelopmentServerEndpoint;
            });

        }

        private static bool IsRunning() => IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(x => x.Port)
            .Contains(Port);
    }
}