using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Exceptionless.Insulation.Configuration {
    /// <summary>
    ///     Extension methods for adding <see cref="YamlConfigurationProvider" />.
    /// </summary>
    public static class YamlConfigurationExtensions {
        /// <summary>
        ///     Adds the YAML configuration provider at <paramref name="path" /> to <paramref name="builder" />.
        /// </summary>
        /// <param name="builder">
        ///     The <see cref="IConfigurationBuilder" /> to add to.
        /// </param>
        /// <param name="path">
        ///     Path relative to the base path stored in <see cref="IConfigurationBuilder.Properties" />
        ///     of <paramref name="builder" />.
        /// </param>
        /// <returns>
        ///     The <see cref="IConfigurationBuilder" />.
        /// </returns>
        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, string path) {
            return AddYamlFile(builder, null, path, false, false);
        }

        /// <summary>
        ///     Adds the YAML configuration provider at <paramref name="path" /> to <paramref name="builder" />.
        /// </summary>
        /// <param name="builder">
        ///     The <see cref="IConfigurationBuilder" /> to add to.
        /// </param>
        /// <param name="path">
        ///     Path relative to the base path stored in <see cref="IConfigurationBuilder.Properties" />
        ///     of <paramref name="builder" />.
        /// </param>
        /// <param name="optional">
        ///     Whether the file is optional.
        /// </param>
        /// <returns>
        ///     The <see cref="IConfigurationBuilder" />.
        /// </returns>
        public static IConfigurationBuilder
            AddYamlFile(this IConfigurationBuilder builder, string path, bool optional) {
            return AddYamlFile(builder, null, path, optional, false);
        }

        /// <summary>
        ///     Adds the YAML configuration provider at <paramref name="path" /> to <paramref name="builder" />.
        /// </summary>
        /// <param name="builder">
        ///     The <see cref="IConfigurationBuilder" /> to add to.
        /// </param>
        /// <param name="path">
        ///     Path relative to the base path stored in <see cref="IConfigurationBuilder.Properties" />
        ///     of <paramref name="builder" />.
        /// </param>
        /// <param name="optional">
        ///     Whether the file is optional.
        /// </param>
        /// <param name="reloadOnChange">
        ///     Whether the configuration should be reloaded if the file changes.
        /// </param>
        /// <returns>
        ///     The <see cref="IConfigurationBuilder" />.
        /// </returns>
        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, string path, bool optional, bool reloadOnChange) {
            return AddYamlFile(builder, null, path, optional, reloadOnChange);
        }

        /// <summary>
        ///     Adds a YAML configuration source to <paramref name="builder" />.
        /// </summary>
        /// <param name="builder">
        ///     The <see cref="IConfigurationBuilder" /> to add to.
        /// </param>
        /// <param name="provider">
        ///     The <see cref="IFileProvider" /> to use to access the file.
        /// </param>
        /// <param name="path">
        ///     Path relative to the base path stored in <see cref="IConfigurationBuilder.Properties" />
        ///     of <paramref name="builder" />.
        /// </param>
        /// <param name="optional">
        ///     Whether the file is optional.
        /// </param>
        /// <param name="reloadOnChange">
        ///     Whether the configuration should be reloaded if the file changes.
        /// </param>
        /// <returns>
        ///     The <see cref="IConfigurationBuilder" />.
        /// </returns>
        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, IFileProvider provider, string path, bool optional, bool reloadOnChange) {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("File path must be a non-empty string.", nameof(path));

            return builder.AddYamlFile(s => {
                s.FileProvider = provider;
                s.Path = path;
                s.Optional = optional;
                s.ReloadOnChange = reloadOnChange;
                s.ResolveFileProvider();
            });
        }

        /// <summary>
        ///     Adds a YAML configuration source to <paramref name="builder" />.
        /// </summary>
        /// <param name="builder">
        ///     The <see cref="IConfigurationBuilder" /> to add to.
        /// </param>
        /// <param name="configureSource">
        ///     Configures the source.
        /// </param>
        /// <returns>
        ///     The <see cref="IConfigurationBuilder" />.
        /// </returns>
        public static IConfigurationBuilder AddYamlFile(
            this IConfigurationBuilder builder,
            Action<YamlConfigurationSource> configureSource) {
            return builder.Add(configureSource);
        }
    }
}