using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    [NLogConfigurationItem]
    public class ElasticSearchField
    {
        [RequiredParameter]
        public string Name { get; set; }

        [RequiredParameter]
        public Layout Layout { get; set; }
    }
}