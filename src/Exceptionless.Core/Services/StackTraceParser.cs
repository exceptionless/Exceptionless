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
        var error = new Error
        {
            Type = exceptionType,
            Message = message,
            StackTrace = []
        };
        InnerError current = error;
        var parents = new Stack<InnerError>();

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

            if (line.Trim().StartsWith("--- End of inner exception", StringComparison.OrdinalIgnoreCase))
            {
                if (parents.TryPop(out var parent))
                    current = parent;
                continue;
            }

            if (TryParseFrame(line, out var frame))
                current.StackTrace ??= [];
            if (frame is not null)
                current.StackTrace!.Add(frame);
        }

        return error;
    }

    public bool TryFindFrame(
        string stackTrace,
        Func<StackFrame, bool> predicate,
        out StackFrame? firstFrame,
        out StackFrame? matchingFrame,
        out string? innermostExceptionType)
    {
        firstFrame = null;
        matchingFrame = null;
        innermostExceptionType = null;

        ReadOnlySpan<char> remaining = stackTrace.AsSpan();
        while (!remaining.IsEmpty)
        {
            int newline = remaining.IndexOf('\n');
            ReadOnlySpan<char> line = newline >= 0 ? remaining[..newline] : remaining;
            remaining = newline >= 0 ? remaining[(newline + 1)..] : [];

            if (TryParseInnerExceptionHeader(line, out string? innerType, out _))
            {
                firstFrame = null;
                matchingFrame = null;
                innermostExceptionType = innerType;
                continue;
            }

            if (!TryParseFrame(line, out var frame))
                continue;

            firstFrame ??= frame;
            if (matchingFrame is null && predicate(frame))
                matchingFrame = frame;
        }

        return firstFrame is not null;
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
}
