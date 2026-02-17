---
name: .NET CLI
description: |
  .NET command-line tools for building, testing, and formatting. Common dotnet commands
  and development workflow.
  Keywords: dotnet build, dotnet restore, dotnet test, dotnet format, dotnet run,
  NuGet, package restore, CLI commands, build system
---

# .NET CLI

## Prerequisites

- **.NET SDK 10.0**
- NuGet feeds defined in `NuGet.Config`

## Common Commands

### Restore Packages

```bash
dotnet restore
```

### Build Solution

```bash
dotnet build
```

### Run Tests

```bash
# All tests
dotnet test

# By test name
dotnet test --filter "FullyQualifiedName~CanCreateOrganization"

# By class name
dotnet test --filter "ClassName~OrganizationTests"

# By category/trait
dotnet test --filter "Category=Integration"
```

### Run Project

```bash
# Run the AppHost (recommended for full stack)
dotnet run --project src/Exceptionless.AppHost

# Run specific project
dotnet run --project src/Exceptionless.Web
```

### Format Code

```bash
# Format all C# files
dotnet format

# Check without making changes
dotnet format --verify-no-changes
```

## NuGet Configuration

Feeds are defined in [NuGet.Config](NuGet.Config) — do not add new sources unless explicitly requested.

## Directory.Build.props

Shared settings live in `src/Directory.Build.props`:

- Target framework versions
- Common package references
- Build properties

Keep changes consistent across the solution.

## Build Configurations

```bash
# Debug build (default)
dotnet build

# Release build
dotnet build -c Release

# Clean and rebuild
dotnet clean && dotnet build
```

## Watch Mode

```bash
# Run with hot reload
dotnet watch run --project src/Exceptionless.Web
```

## Package Management

```bash
# Add package to project
dotnet add package Foundatio

# Remove package
dotnet remove package OldPackage

# List packages
dotnet list package

# Check for outdated packages
dotnet list package --outdated
```

## Solution Management

```bash
# Build specific project
dotnet build src/Exceptionless.Core

# List projects in solution
dotnet sln list
```

## Environment Variables

```bash
# Set environment for run
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Exceptionless.Web
```

## Troubleshooting

### Clean Restore

```bash
# Clear NuGet cache and restore
dotnet nuget locals all --clear
dotnet restore
```

### Verbose Build

```bash
dotnet build -v detailed
```
