using Microsoft.Extensions.Logging;

namespace Exceptionless.Web.Extensions {
    public static class SerilogExtensions {
        public static ILoggerFactory ToLoggerFactory(this Serilog.ILogger logger) {
            return new Serilog.Extensions.Logging.SerilogLoggerFactory(logger);
        }
    }
}
