﻿using Exceptionless.Core.Models;
using McSherry.SemanticVersioning;

namespace Exceptionless.Core.Extensions;

public static class StackExtensions
{
    public static void MarkFixed(this Stack stack, SemanticVersion? version, TimeProvider timeProvider)
    {
        stack.Status = StackStatus.Fixed;
        stack.DateFixed = timeProvider.GetUtcNow().UtcDateTime;
        stack.FixedInVersion = version?.ToString();
        stack.SnoozeUntilUtc = null;
    }

    public static void MarkOpen(this Stack stack)
    {
        stack.Status = StackStatus.Open;
        stack.DateFixed = null;
        stack.FixedInVersion = null;
        stack.SnoozeUntilUtc = null;
    }

    public static Stack ApplyOffset(this Stack stack, TimeSpan offset)
    {
        if (stack.DateFixed.HasValue)
            stack.DateFixed = stack.DateFixed.Value.Add(offset);

        if (stack.FirstOccurrence != DateTime.MinValue)
            stack.FirstOccurrence = stack.FirstOccurrence.Add(offset);

        if (stack.LastOccurrence != DateTime.MinValue)
            stack.LastOccurrence = stack.LastOccurrence.Add(offset);

        return stack;
    }

    public static string? GetTypeName(this Stack stack)
    {
        if (stack.SignatureInfo.TryGetValue("ExceptionType", out string? type) && !String.IsNullOrEmpty(type))
            return type.TypeName();

        return type;
    }

    public static bool IsFixed(this Stack stack)
    {
        return stack.Status == StackStatus.Fixed;
    }

    public static bool Is404(this Stack stack)
    {
        return stack.SignatureInfo.ContainsKey("HttpMethod") && stack.SignatureInfo.ContainsKey("Path");
    }
}
