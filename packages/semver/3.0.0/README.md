A Semantic Version Library for .NET
===================================

Create, parse, and manipulate semantic version numbers with the `SemVersion` class and semantic
version ranges with the `SemVersionRange` class. This library complies with v2.0.0 of the semantic
versioning spec from [semver.org](http://semver.org).

API docs for the most recent release are available online at [semver-nuget.org](https://semver-nuget.org/).

## Parsing

```csharp
var version = SemVersion.Parse("1.1.0-rc.1+e471d15", SemVersionStyles.Strict);
```

## Constructing

```csharp
var v1 = new SemVersion(1, 0);
var vNextRc = SemVersion.ParsedFrom(1, 1, 0, "rc.1");
```

## Comparing

```csharp
if (version.ComparePrecedenceTo(vNextRc) == 0)
    Console.WriteLine($"{version} has the same precedence as {vNextRc}");

if (version.CompareSortOrderTo(vNextRc) > 0)
    Console.WriteLine($"{version} sorts after {vNextRc}");
```

Outputs:

```text
1.1.0-rc.1+e471d15 has the same precedence as 1.1.0-rc.1
1.1.0-rc.1+e471d15 sorts after 1.1.0-rc.1
```

## Manipulating

```csharp
Console.WriteLine($"Current: {version}");
if (version.IsPrerelease)
{
    Console.WriteLine($"Prerelease: {version.Prerelease}");
    Console.WriteLine($"Next release version is: {version.WithoutPrereleaseOrMetadata()}");
}
```

Outputs:

```text
Current: 1.1.0-rc.1+e471d15
Prerelease: rc.1
Next release version is: 1.1.0
```

## Version Ranges

```csharp
var range = SemVersionRange.Parse("^1.0.0");
var prereleaseRange = SemVersionRange.ParseNpm("^1.0.0", includeAllPrerelease: true);
Console.WriteLine($"Range: {range}");
Console.WriteLine($"Prerelease range: {prereleaseRange}");
Console.WriteLine($"Range includes version {version}: {range.Contains(version)}");
Console.WriteLine($"Prerelease range includes version {version}: {prereleaseRange.Contains(version)}");

// Alternative: another way to call SemVersionRange.Contains(version)
version.Satisfies(range);

// Alternative: slower because it parses the range on every call
version.SatisfiesNpm("^1.0.0", includeAllPrerelease: true)
```

Outputs:

```text
Range: 1.*
Prerelease range: *-* 1.*
Range includes version 1.1.0-rc.1+e471d15: False
Prerelease range includes version 1.1.0-rc.1+e471d15: True
```
