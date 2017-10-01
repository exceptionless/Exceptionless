using Microsoft.Extensions.Configuration;

namespace Exceptionless.Insulation.Configuration {
    public class YamlConfigurationSource : FileConfigurationSource {
        public override IConfigurationProvider Build(IConfigurationBuilder builder) {
            EnsureDefaults(builder);
            return new YamlConfigurationProvider(this);
        }
    }
}