using System.Text.Json;

namespace Exceptionless.Core.Services.SourceMaps;

public sealed class SourceMapDocument
{
    private const string Base64Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    private const int MaximumSourceEntries = 1_000_000;
    private readonly IReadOnlyList<IReadOnlyList<MappingSegment>> _lines;
    private readonly string[] _names;
    private readonly string[] _sources;

    private SourceMapDocument(
        string? sourceRoot,
        string[] sources,
        string[] names,
        IReadOnlyList<IReadOnlyList<MappingSegment>> lines,
        long estimatedMemorySize)
    {
        SourceRoot = sourceRoot;
        _sources = sources;
        _names = names;
        _lines = lines;
        EstimatedMemorySize = estimatedMemorySize;
    }

    public string? SourceRoot { get; }
    internal long EstimatedMemorySize { get; }

    public static SourceMapDocument Parse(byte[] sourceMap, int maximumSegments = 1_000_000)
        => Parse(sourceMap, maximumSegments, 100_000, MaximumSourceEntries);

    internal static SourceMapDocument Parse(byte[] sourceMap, int maximumSegments, int maximumLines, int maximumSourceEntries = MaximumSourceEntries)
    {
        using var document = JsonDocument.Parse(sourceMap);
        var root = document.RootElement;

        if (!root.TryGetProperty("version", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out int version)
            || version != 3)
            throw new JsonException("Only source map version 3 is supported.");

        if (root.TryGetProperty("sections", out _))
            throw new JsonException("Indexed source maps are not supported.");

        if (!root.TryGetProperty("mappings", out var mappingsElement) || mappingsElement.ValueKind != JsonValueKind.String)
            throw new JsonException("The source map mappings are required.");

        string mappings = mappingsElement.GetString() ?? throw new JsonException("The source map mappings are required.");
        string[] sources = ReadStringArray(root, "sources", maximumSourceEntries, out long sourcesMemorySize);
        long namesMemorySize = 0;
        string[] names = root.TryGetProperty("names", out _)
            ? ReadStringArray(root, "names", maximumSourceEntries, out namesMemorySize)
            : [];
        string? sourceRoot = root.TryGetProperty("sourceRoot", out var sourceRootElement) ? sourceRootElement.GetString() : null;

        var lines = DecodeMappings(mappings, sources.Length, names.Length, maximumSegments, maximumLines, out int segmentCount);
        long estimatedMemorySize = sourceMap.LongLength
            + sourcesMemorySize
            + namesMemorySize
            + EstimateStringMemorySize(sourceRoot)
            + (segmentCount * 64L)
            + (lines.Count * 64L);
        return new SourceMapDocument(sourceRoot, sources, names, lines, estimatedMemorySize);
    }

    public SourceMapLocation? FindOriginalLocation(int generatedLine, int generatedColumn)
    {
        if (generatedLine < 0 || generatedLine >= _lines.Count || generatedColumn < 0)
            return null;

        var segments = _lines[generatedLine];
        MappingSegment? match = null;
        foreach (var segment in segments)
        {
            if (segment.GeneratedColumn > generatedColumn)
                break;

            match = segment;
        }

        if (match?.SourceIndex is not int sourceIndex || match.OriginalLine is not int originalLine || match.OriginalColumn is not int originalColumn)
            return null;

        string? name = match.NameIndex is int nameIndex ? _names[nameIndex] : null;
        return new SourceMapLocation(CombineSource(SourceRoot, _sources[sourceIndex]), originalLine, originalColumn, name);
    }

    private static string[] ReadStringArray(JsonElement root, string propertyName, int maximumEntries, out long estimatedMemorySize)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
            throw new JsonException($"The source map {propertyName} array is required.");
        if (maximumEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));

        estimatedMemorySize = 0;
        var values = new List<string>(Math.Min(element.GetArrayLength(), maximumEntries));
        foreach (var value in element.EnumerateArray())
        {
            if (values.Count >= maximumEntries)
                throw new JsonException($"The source map {propertyName} array contains too many entries.");

            string text = value.GetString() ?? throw new JsonException($"The source map {propertyName} array contains a null value.");
            values.Add(text);
            estimatedMemorySize += IntPtr.Size + EstimateStringMemorySize(text);
        }

        return values.ToArray();
    }

    private static long EstimateStringMemorySize(string? value) => value is null ? 0 : 24L + (value.Length * sizeof(char));

    private static IReadOnlyList<IReadOnlyList<MappingSegment>> DecodeMappings(
        string mappings,
        int sourceCount,
        int nameCount,
        int maximumSegments,
        int maximumLines,
        out int segmentCount)
    {
        if (maximumSegments < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumSegments));
        if (maximumLines < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumLines));

        int lineCount = 1;
        int encodedSegmentCount = 0;
        bool hasEncodedSegment = false;
        foreach (char character in mappings)
        {
            if (character is ',' or ';')
            {
                if (hasEncodedSegment && ++encodedSegmentCount > maximumSegments)
                    throw new JsonException("The source map contains too many mapping segments.");
                hasEncodedSegment = false;
            }
            else
            {
                hasEncodedSegment = true;
            }

            if (character == ';' && ++lineCount > maximumLines)
                throw new JsonException("The source map contains too many generated lines.");
        }
        if (hasEncodedSegment && ++encodedSegmentCount > maximumSegments)
            throw new JsonException("The source map contains too many mapping segments.");

        var lines = new List<IReadOnlyList<MappingSegment>>(lineCount);
        int sourceIndex = 0;
        int originalLine = 0;
        int originalColumn = 0;
        int nameIndex = 0;
        segmentCount = 0;

        foreach (string encodedLine in mappings.Split(';'))
        {
            int generatedColumn = 0;
            var line = new List<MappingSegment>();
            foreach (string encodedSegment in encodedLine.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (++segmentCount > maximumSegments)
                    throw new JsonException("The source map contains too many mapping segments.");

                int index = 0;
                generatedColumn += DecodeValue(encodedSegment, ref index);
                if (generatedColumn < 0)
                    throw new JsonException("A source map generated column cannot be negative.");

                if (index == encodedSegment.Length)
                {
                    line.Add(new MappingSegment(generatedColumn, null, null, null, null));
                    continue;
                }

                sourceIndex += DecodeValue(encodedSegment, ref index);
                originalLine += DecodeValue(encodedSegment, ref index);
                originalColumn += DecodeValue(encodedSegment, ref index);
                if (sourceIndex < 0 || sourceIndex >= sourceCount || originalLine < 0 || originalColumn < 0)
                    throw new JsonException("A source map mapping points outside its source data.");

                int? segmentNameIndex = null;
                if (index < encodedSegment.Length)
                {
                    nameIndex += DecodeValue(encodedSegment, ref index);
                    if (nameIndex < 0 || nameIndex >= nameCount)
                        throw new JsonException("A source map mapping points outside its names array.");

                    segmentNameIndex = nameIndex;
                }

                if (index != encodedSegment.Length)
                    throw new JsonException("A source map segment has an invalid field count.");

                line.Add(new MappingSegment(generatedColumn, sourceIndex, originalLine, originalColumn, segmentNameIndex));
            }

            lines.Add(line);
        }

        return lines;
    }

    private static int DecodeValue(string segment, ref int index)
    {
        long result = 0;
        int shift = 0;
        bool hasContinuation;

        do
        {
            if (index >= segment.Length)
                throw new JsonException("A source map VLQ value is incomplete.");

            int digit = Base64Characters.IndexOf(segment[index++]);
            if (digit < 0)
                throw new JsonException("A source map VLQ value contains an invalid character.");

            hasContinuation = (digit & 32) != 0;
            digit &= 31;
            result += (long)digit << shift;
            shift += 5;
            if (shift > 35 || result > (long)Int32.MaxValue * 2 + 1)
                throw new JsonException("A source map VLQ value is too large.");
        } while (hasContinuation);

        bool isNegative = (result & 1) == 1;
        result >>= 1;
        return isNegative ? -(int)result : (int)result;
    }

    private static string CombineSource(string? sourceRoot, string source)
    {
        if (String.IsNullOrWhiteSpace(sourceRoot) || Uri.TryCreate(source, UriKind.Absolute, out _))
            return source;

        if (Uri.TryCreate(sourceRoot, UriKind.Absolute, out var sourceRootUri))
        {
            var sourceRootBuilder = new UriBuilder(sourceRootUri) { Path = sourceRootUri.AbsolutePath.TrimEnd('/') + '/' };
            if (Uri.TryCreate(sourceRootBuilder.Uri, source.TrimStart('/'), out var combinedUri))
                return combinedUri.ToString();
        }

        return $"{sourceRoot.TrimEnd('/')}/{source.TrimStart('/')}";
    }

    private sealed record MappingSegment(int GeneratedColumn, int? SourceIndex, int? OriginalLine, int? OriginalColumn, int? NameIndex);
}

public sealed record SourceMapLocation(string Source, int Line, int Column, string? Name);
