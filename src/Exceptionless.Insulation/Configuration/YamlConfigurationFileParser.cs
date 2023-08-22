﻿using Microsoft.Extensions.Configuration;
using YamlDotNet.RepresentationModel;

namespace Exceptionless.Insulation.Configuration;

internal class YamlConfigurationFileParser
{
    private readonly Stack<string> _context = new();

    private readonly IDictionary<string, string?> _data = new SortedDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    private string? _currentPath;

    public IDictionary<string, string?> Parse(Stream stream)
    {
        _data.Clear();

        var yamlStream = new YamlStream();
        yamlStream.Load(new StreamReader(stream));

        if (!yamlStream.Documents.Any())
            return _data;

        if (!(yamlStream.Documents[0].RootNode is YamlMappingNode mappingNode))
            return _data;

        foreach (var nodePair in mappingNode.Children)
        {
            string? context = ((YamlScalarNode)nodePair.Key).Value;
            if (context is not null)
                VisitYamlNode(context, nodePair.Value);
        }

        return _data;
    }

    private void VisitYamlNode(string context, YamlNode node)
    {
        switch (node)
        {
            case YamlScalarNode scalarNode:
                VisitYamlScalarNode(context, scalarNode);
                break;
            case YamlMappingNode mappingNode:
                VisitYamlMappingNode(context, mappingNode);
                break;
            case YamlSequenceNode sequenceNode:
                VisitYamlSequenceNode(context, sequenceNode);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(node),
                    $"Unsupported YAML node type '{node.GetType().Name} was found. " +
                    $"Path '{_currentPath}', line {node.Start.Line} position {node.Start.Column}.");
        }
    }

    private void VisitYamlScalarNode(string context, YamlScalarNode scalarNode)
    {
        EnterContext(context);

        string? currentKey = _currentPath;
        if (currentKey is null)
            throw new ArgumentException("key cannot be null");

        if (_data.ContainsKey(currentKey))
            throw new FormatException($"A duplicate key '{currentKey}' was found.");

        _data[currentKey] = scalarNode.Value;
        ExitContext();
    }

    private void VisitYamlMappingNode(string context, YamlMappingNode mappingNode)
    {
        EnterContext(context);

        foreach (var nodePair in mappingNode.Children)
        {
            string? innerContext = ((YamlScalarNode)nodePair.Key).Value;
            if (innerContext is not null)
                VisitYamlNode(innerContext, nodePair.Value);
        }

        ExitContext();
    }

    private void VisitYamlSequenceNode(string context, YamlSequenceNode sequenceNode)
    {
        EnterContext(context);

        for (int i = 0; i < sequenceNode.Children.Count; ++i)
            VisitYamlNode(i.ToString(), sequenceNode.Children[i]);

        ExitContext();
    }

    private void EnterContext(string context)
    {
        _context.Push(context);
        _currentPath = ConfigurationPath.Combine(_context.Reverse());
    }

    private void ExitContext()
    {
        _context.Pop();
        _currentPath = ConfigurationPath.Combine(_context.Reverse());
    }
}
