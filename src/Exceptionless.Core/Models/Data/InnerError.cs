﻿using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data;

public class InnerError : IData
{
    /// <summary>
    /// The error message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The error type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The error code.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Extended data entries for this error.
    /// </summary>
    public DataDictionary? Data { get; set; } = new();

    /// <summary>
    /// An inner (nested) error.
    /// </summary>
    public InnerError? Inner { get; set; }

    /// <summary>
    /// The stack trace for the error.
    /// </summary>
    public StackFrameCollection? StackTrace { get; set; } = new();

    /// <summary>
    /// The target method.
    /// </summary>
    public Method? TargetMethod { get; set; }

    protected bool Equals(InnerError other)
    {
        return String.Equals(Message, other.Message) && String.Equals(Type, other.Type) && String.Equals(Code, other.Code) && Equals(Data, other.Data) && Equals(Inner, other.Inner) && StackTrace.CollectionEquals(other.StackTrace) && Equals(TargetMethod, other.TargetMethod);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((InnerError)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Message?.GetHashCode() ?? 0;
            hashCode = (hashCode * 397) ^ (Type?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Code?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Data?.GetCollectionHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Inner?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (StackTrace?.GetCollectionHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (TargetMethod?.GetHashCode() ?? 0);
            return hashCode;
        }
    }
}
