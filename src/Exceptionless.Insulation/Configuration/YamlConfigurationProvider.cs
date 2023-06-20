﻿using Microsoft.Extensions.Configuration;
using YamlDotNet.Core;

namespace Exceptionless.Insulation.Configuration;

public class YamlConfigurationProvider : FileConfigurationProvider
{
    public YamlConfigurationProvider(YamlConfigurationSource source)
        : base(source)
    {
    }

    public override void Load(Stream stream)
    {
        var parser = new YamlConfigurationFileParser();
        try
        {
            Data = parser.Parse(stream);
        }
        catch (YamlException ex)
        {
            string errorLine = String.Empty;
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);

                using (var streamReader = new StreamReader(stream))
                {
                    var fileContent = ReadLines(streamReader);
                    errorLine = RetrieveErrorContext(ex, fileContent);
                }
            }

            throw new FormatException(
                "Could not parse the YAML file. " +
                $"Error on line number '{ex.Start.Line}': '{errorLine}'.", ex);
        }
    }

    private static string RetrieveErrorContext(YamlException ex, IEnumerable<string> fileContent)
    {
        string possibleLineContent = fileContent.Skip(ex.Start.Line - 1).FirstOrDefault();
        return possibleLineContent ?? String.Empty;
    }

    private static IEnumerable<string> ReadLines(StreamReader streamReader)
    {
        string line;
        do
        {
            line = streamReader.ReadLine();
            yield return line;
        } while (line != null);
    }
}
