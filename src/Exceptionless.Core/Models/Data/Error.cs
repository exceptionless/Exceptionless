﻿using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data;

public class Error : InnerError
{
    /// <summary>
    /// Any modules that were loaded / referenced when the error occurred.
    /// </summary>
    public ModuleCollection Modules { get; set; } = new();

    public static class KnownDataKeys
    {
        public const string ExtraProperties = "@ext";
        public const string TargetInfo = "@target";
    }

    protected bool Equals(Error other)
    {
        return base.Equals(other) && Modules.CollectionEquals(other.Modules);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((Error)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (base.GetHashCode() * 397) ^ (Modules?.GetCollectionHashCode() ?? 0);
        }
    }
}
