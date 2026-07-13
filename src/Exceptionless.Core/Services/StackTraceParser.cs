using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Services;

public sealed class StackTraceParser
{
    public StackFrameCollection Parse(string stackTrace)
    {
        var frames = new StackFrameCollection();
        ReadOnlySpan<char> remaining = stackTrace.AsSpan();
        while (!remaining.IsEmpty)
        {
            int newline = remaining.IndexOf('\n');
            ReadOnlySpan<char> line = newline >= 0 ? remaining[..newline] : remaining;
            remaining = newline >= 0 ? remaining[(newline + 1)..] : [];

            if (TryParseFrame(line, out var frame))
                frames.Add(frame);
        }

        return frames;
    }

    public Error ParseError(string stackTrace, string? exceptionType, string? message)
    {
        return ParseErrorWithCoverage(stackTrace, exceptionType, message).Error;
    }

    public StackTraceParseResult ParseErrorWithCoverage(string stackTrace, string? exceptionType, string? message)
    {
        var error = new Error
        {
            Type = exceptionType,
            Message = message,
            StackTrace = []
        };
        InnerError current = error;
        var parents = new Stack<InnerError>();
        bool isComplete = true;

        ReadOnlySpan<char> remaining = stackTrace.AsSpan();
        while (!remaining.IsEmpty)
        {
            int newline = remaining.IndexOf('\n');
            ReadOnlySpan<char> line = newline >= 0 ? remaining[..newline] : remaining;
            remaining = newline >= 0 ? remaining[(newline + 1)..] : [];

            if (TryParseInnerExceptionHeader(line, out string? innerType, out string? innerMessage))
            {
                var inner = new InnerError { Type = innerType, Message = innerMessage, StackTrace = [] };
                current.Inner = inner;
                parents.Push(current);
                current = inner;
                continue;
            }

            ReadOnlySpan<char> trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("--- End of inner exception", StringComparison.OrdinalIgnoreCase))
            {
                if (parents.TryPop(out var parent))
                    current = parent;
                continue;
            }

            if (TryParseFrame(line, out var frame))
            {
                current.StackTrace ??= [];
                current.StackTrace!.Add(frame);
                continue;
            }

            if (!trimmedLine.IsEmpty && !IsOuterExceptionHeader(trimmedLine, exceptionType))
                isComplete = false;
        }

        return new StackTraceParseResult(error, isComplete);
    }

    public bool TryFindFrame(
        string stackTrace,
        Func<StackFrame, bool> predicate,
        out StackFrame? firstFrame,
        out StackFrame? matchingFrame,
        out string? selectedExceptionType)
    {
        firstFrame = null;
        matchingFrame = null;
        selectedExceptionType = null;

        // Preserve the exception nesting while retaining only the two frames needed for
        // fingerprinting. ErrorSignature examines user frames from the innermost exception
        // outward, then falls back to the innermost exception's first frame. A flat scan can
        // incorrectly combine an inner exception type with an outer user frame.
        var root = new FingerprintSegment(null);
        var segments = new List<FingerprintSegment> { root };
        var parents = new Stack<FingerprintSegment>();
        FingerprintSegment current = root;

        ReadOnlySpan<char> remaining = stackTrace.AsSpan();
        while (!remaining.IsEmpty)
        {
            int newline = remaining.IndexOf('\n');
            ReadOnlySpan<char> line = newline >= 0 ? remaining[..newline] : remaining;
            remaining = newline >= 0 ? remaining[(newline + 1)..] : [];

            if (TryParseInnerExceptionHeader(line, out string? innerType, out _))
            {
                var inner = new FingerprintSegment(innerType);
                segments.Add(inner);
                parents.Push(current);
                current = inner;
                continue;
            }

            ReadOnlySpan<char> trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("--- End of inner exception", StringComparison.OrdinalIgnoreCase))
            {
                if (parents.TryPop(out var parent))
                    current = parent;
                continue;
            }

            if (!TryParseFrame(line, out var frame))
                continue;

            current.FirstFrame ??= frame;
            if (current.MatchingFrame is null && predicate(frame))
                current.MatchingFrame = frame;
        }

        for (int index = segments.Count - 1; index >= 0; index--)
        {
            if (segments[index].MatchingFrame is null)
                continue;

            matchingFrame = segments[index].MatchingFrame;
            selectedExceptionType = segments[index].ExceptionType;
            break;
        }

        FingerprintSegment innermost = segments[^1];
        firstFrame = innermost.FirstFrame;
        if (matchingFrame is null)
            selectedExceptionType = innermost.ExceptionType;

        return firstFrame is not null || matchingFrame is not null;
    }

    internal static bool TryParseFrame(ReadOnlySpan<char> rawLine, out StackFrame frame)
    {
        frame = null!;
        ReadOnlySpan<char> line = rawLine.Trim();
        if (line.IsEmpty)
            return false;

        if (line.StartsWith("File \"", StringComparison.Ordinal))
            return TryParsePythonFrame(line, out frame);

        ReadOnlySpan<char> methodPart;
        ReadOnlySpan<char> locationPart = [];
        if (line.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
        {
            line = line[3..].TrimStart();
            methodPart = line;
        }
        else if (line.StartsWith("#", StringComparison.Ordinal))
        {
            int space = line.IndexOf(' ');
            if (space < 0)
                return false;
            line = line[(space + 1)..].TrimStart();
            methodPart = line;
        }
        else
        {
            int atSign = line.IndexOf('@');
            if (atSign <= 0)
                return false;
            methodPart = line[..atSign];
            locationPart = line[(atSign + 1)..];
        }

        int inIndex = methodPart.IndexOf(" in ", StringComparison.Ordinal);
        if (locationPart.IsEmpty && inIndex > 0)
        {
            locationPart = methodPart[(inIndex + 4)..];
            methodPart = methodPart[..inIndex];
        }
        else if (locationPart.IsEmpty && methodPart.EndsWith(')'))
        {
            int openParen = methodPart.LastIndexOf('(');
            if (openParen > 0)
            {
                ReadOnlySpan<char> candidate = methodPart[(openParen + 1)..^1];
                if (candidate.Contains(':') || candidate.Contains('/') || candidate.Contains(".java", StringComparison.OrdinalIgnoreCase))
                {
                    methodPart = methodPart[..openParen];
                    locationPart = candidate;
                }
            }
        }

        methodPart = StripParameters(methodPart.Trim());
        if (methodPart.StartsWith("async ", StringComparison.Ordinal))
            methodPart = methodPart[6..].TrimStart();

        if (methodPart.IsEmpty || methodPart.SequenceEqual("<anonymous>"))
            return false;

        frame = CreateFrame(methodPart);
        ApplyLocation(frame, locationPart);
        return !String.IsNullOrEmpty(frame.Name);
    }

    private static bool TryParsePythonFrame(ReadOnlySpan<char> line, out StackFrame frame)
    {
        frame = null!;
        int fileEnd = line[6..].IndexOf('"');
        if (fileEnd < 0)
            return false;

        fileEnd += 6;
        string fileName = line[6..fileEnd].ToString();
        ReadOnlySpan<char> remainder = line[(fileEnd + 1)..];
        int inIndex = remainder.IndexOf(" in ", StringComparison.Ordinal);
        ReadOnlySpan<char> method = inIndex >= 0 ? remainder[(inIndex + 4)..].Trim() : "<module>";

        frame = CreateFrame(method);
        frame.FileName = fileName;

        int lineIndex = remainder.IndexOf("line ", StringComparison.Ordinal);
        if (lineIndex >= 0)
        {
            ReadOnlySpan<char> number = remainder[(lineIndex + 5)..];
            int comma = number.IndexOf(',');
            if (comma >= 0)
                number = number[..comma];
            if (Int32.TryParse(number, out int lineNumber))
                frame.LineNumber = lineNumber;
        }

        return true;
    }

    private static bool TryParseInnerExceptionHeader(ReadOnlySpan<char> rawLine, out string? exceptionType, out string? message)
    {
        exceptionType = null;
        message = null;
        ReadOnlySpan<char> line = rawLine.Trim();
        if (line.StartsWith("Caused by:", StringComparison.OrdinalIgnoreCase))
        {
            line = line[10..].TrimStart();
        }
        else
        {
            int arrow = line.IndexOf("---> ", StringComparison.Ordinal);
            if (arrow < 0)
                return false;
            line = line[(arrow + 5)..].TrimStart();
        }

        int separator = line.IndexOf(':');
        ReadOnlySpan<char> type = separator >= 0 ? line[..separator].Trim() : line;
        if (type.IsEmpty || type.Contains(' '))
            return false;

        exceptionType = type.ToString();
        if (separator >= 0)
        {
            ReadOnlySpan<char> messageValue = line[(separator + 1)..].Trim();
            if (!messageValue.IsEmpty)
                message = messageValue.ToString();
        }

        return true;
    }

    private static bool IsOuterExceptionHeader(ReadOnlySpan<char> line, string? exceptionType)
    {
        if (String.IsNullOrWhiteSpace(exceptionType)
            || !line.StartsWith(exceptionType.AsSpan(), StringComparison.Ordinal))
            return false;

        ReadOnlySpan<char> remainder = line[exceptionType.Length..];
        return remainder.IsEmpty || remainder[0] == ':';
    }

    private static ReadOnlySpan<char> StripParameters(ReadOnlySpan<char> method)
    {
        int openParen = method.IndexOf('(');
        return openParen > 0 ? method[..openParen] : method;
    }

    private static StackFrame CreateFrame(ReadOnlySpan<char> method)
    {
        int lastDot = method.LastIndexOf('.');
        if (lastDot < 0)
            return new StackFrame { Name = method.ToString() };

        ReadOnlySpan<char> name = method[(lastDot + 1)..];
        ReadOnlySpan<char> declaring = method[..lastDot];
        int typeDot = declaring.LastIndexOf('.');

        return new StackFrame
        {
            Name = name.ToString(),
            DeclaringType = typeDot >= 0 ? declaring[(typeDot + 1)..].ToString() : declaring.ToString(),
            DeclaringNamespace = typeDot >= 0 ? declaring[..typeDot].ToString() : null
        };
    }

    private static void ApplyLocation(StackFrame frame, ReadOnlySpan<char> location)
    {
        location = location.Trim();
        if (location.IsEmpty || location.SequenceEqual("Unknown Source") || location.SequenceEqual("Native Method"))
            return;

        int lineMarker = location.LastIndexOf(":line ", StringComparison.Ordinal);
        if (lineMarker >= 0)
        {
            frame.FileName = location[..lineMarker].ToString();
            if (Int32.TryParse(location[(lineMarker + 6)..], out int lineNumber))
                frame.LineNumber = lineNumber;
            return;
        }

        int lastColon = location.LastIndexOf(':');
        if (lastColon > 0 && Int32.TryParse(location[(lastColon + 1)..], out int finalNumber))
        {
            ReadOnlySpan<char> beforeFinal = location[..lastColon];
            int previousColon = beforeFinal.LastIndexOf(':');
            if (previousColon > 0 && Int32.TryParse(beforeFinal[(previousColon + 1)..], out int lineNumber))
            {
                frame.FileName = beforeFinal[..previousColon].ToString();
                frame.LineNumber = lineNumber;
                frame.Column = finalNumber;
            }
            else
            {
                frame.FileName = beforeFinal.ToString();
                frame.LineNumber = finalNumber;
            }
            return;
        }

        frame.FileName = location.ToString();
    }

    private sealed class FingerprintSegment(string? exceptionType)
    {
        public string? ExceptionType { get; } = exceptionType;
        public StackFrame? FirstFrame { get; set; }
        public StackFrame? MatchingFrame { get; set; }
    }
}

public sealed record StackTraceParseResult(Error Error, bool IsComplete);
