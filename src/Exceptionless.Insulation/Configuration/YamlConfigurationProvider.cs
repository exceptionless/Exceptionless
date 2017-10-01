using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Core;

namespace Exceptionless.Insulation.Configuration {
    public class YamlConfigurationProvider : FileConfigurationProvider {
        public YamlConfigurationProvider(YamlConfigurationSource source)
            : base(source) {
        }

        public override void Load(Stream stream) {
            var parser = new YamlConfigurationFileParser();
            try {
                Data = parser.Parse(stream);
            }
            catch (YamlException ex) {
                var errorLine = string.Empty;
                if (stream.CanSeek) {
                    stream.Seek(0, SeekOrigin.Begin);

                    using (var streamReader = new StreamReader(stream)) {
                        var fileContent = ReadLines(streamReader);
                        errorLine = RetrieveErrorContext(ex, fileContent);
                    }
                }

                throw new FormatException(
                    "Could not parse the YAML file. " +
                    $"Error on line number '{ex.Start.Line}': '{errorLine}'.", ex);
            }
        }

        private static string RetrieveErrorContext(YamlException ex, IEnumerable<string> fileContent) {
            var possibleLineContent = fileContent.Skip(ex.Start.Line - 1).FirstOrDefault();
            return possibleLineContent ?? string.Empty;
        }

        private static IEnumerable<string> ReadLines(StreamReader streamReader) {
            string line;
            do {
                line = streamReader.ReadLine();
                yield return line;
            } while (line != null);
        }
    }
}