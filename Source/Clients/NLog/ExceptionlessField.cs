using NLog.Config;
using NLog.Layouts;

namespace Exceptionless.NLog {
    [NLogConfigurationItem]
    public class ExceptionlessField {
        [RequiredParameter]
        public string Name { get; set; }

        [RequiredParameter]
        public Layout Layout { get; set; }
    }
}