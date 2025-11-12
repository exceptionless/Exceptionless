# .NET 10 and Aspire 13 Upgrade Notes

## Upgrade Summary

Successfully upgraded from .NET 9 and Aspire 9.5 to .NET 10 and Aspire 13.0.0.

## New Features Available

### .NET 10 Features

#### Performance Improvements
- **JIT Inlining Enhancements**: Improved JIT compiler inlining decisions for better runtime performance
- **Cryptography Performance**: Significant performance improvements in cryptographic operations
- **Runtime Optimizations**: Enhanced garbage collection and memory management

#### Container Support
- **Streamlined Containerization**: Improved workflows for building and deploying containerized applications
- **Better Docker Integration**: Enhanced support for multi-stage builds and optimized images

#### C# 14 Language Features
- **File-based Apps**: Support for single-file C# scripts that can be executed directly from the CLI
- **Enhanced Pattern Matching**: New pattern matching capabilities for cleaner code
- **Primary Constructors**: Simplified class constructor syntax

#### API Improvements
- **Minimal APIs**: Enhanced minimal API support with better parameter binding
- **Native AOT**: Continued improvements to Native AOT compilation for faster startup and smaller deployments
- **OpenTelemetry**: Enhanced built-in telemetry and observability features

### Aspire 13 Features

#### Polyglot Application Platform
- **First-class JavaScript Support**: JavaScript apps are now first-class citizens alongside .NET and Python
- **AddJavaScriptApp API**: New unified API for orchestrating npm, yarn, and pnpm-based applications
- **Automatic Package Manager Detection**: Smart detection of the package manager used by JavaScript projects

#### Enhanced Orchestration
- **Improved Vite Support**: Better hot reload, port mapping, and Dockerfile generation for Vite apps
- **Multi-stage Docker Publishing**: Optimized Dockerfile generation with Node version detection
- **Static Port Configuration**: Ability to specify static host ports for consistent callback URLs

#### Eventing Model
- **New Eventing Infrastructure**: Modernized eventing system replacing lifecycle hooks
- **Resource-specific Events**: More granular events like `BeforeResourceStartedEvent` and `ResourceEndpointsAllocatedEvent`
- **Better Event Subscription**: Improved patterns for subscribing to application events

#### Developer Experience
- **Enhanced Dashboard**: Improved Aspire dashboard for monitoring distributed applications
- **Better Debugging**: Enhanced debugging experience for distributed applications
- **Streamlined Local Development**: Improved local development experience with automatic port allocation

## Breaking Changes Addressed

### Aspire 13 Breaking Changes
1. **Package Rename**: `Aspire.Hosting.NodeJs` → `Aspire.Hosting.JavaScript`
2. **API Change**: `AddNpmApp` → `AddJavaScriptApp` with automatic package manager detection
3. **Lifecycle Hooks**: Old lifecycle hook system deprecated in favor of new eventing model
4. **Endpoint API**: `WithEndpoint` parameters changed to use `WithHttpEndpoint`

### .NET 10 Breaking Changes
1. **ForwardedHeaders**: `KnownNetworks` property renamed to `KnownIPNetworks`
2. **Framework Packages**: Several packages (System.Net.Http, System.Text.RegularExpressions, System.Text.Encodings.Web) are now included in the framework and should be removed from project references
3. **HealthChecks**: Microsoft.Extensions.Diagnostics.HealthChecks is now included in ASP.NET Core and doesn't need explicit package reference

## Recommendations for Future Work

### Immediate Improvements
1. **Update KibanaConfigWriterHook**: Migrate to Aspire 13's new eventing model
2. **Leverage C# 14 Features**: Consider using primary constructors and enhanced pattern matching in new code
3. **OpenTelemetry Integration**: Take advantage of enhanced built-in telemetry features

### Performance Optimizations
1. **Review Cryptography Usage**: Update crypto operations to leverage .NET 10 performance improvements
2. **Container Optimization**: Review Dockerfiles to take advantage of new multi-stage build optimizations
3. **JIT Compiler Benefits**: Profile hot paths to measure JIT inlining improvements

### Developer Experience
1. **Aspire Dashboard**: Utilize the enhanced dashboard for better observability during development
2. **JavaScript Integration**: Consider migrating to AddJavaScriptApp pattern for better package manager support
3. **Static Ports**: Use static port configuration for better development consistency

## Migration Notes

### Completed
- ✅ All projects targeting net10.0
- ✅ Aspire packages updated to 13.0.0
- ✅ Microsoft.Extensions packages updated to 10.0.0
- ✅ Docker images updated to .NET 10
- ✅ GitHub Actions workflows updated to .NET 10
- ✅ Deprecated APIs replaced

### Pending
- ⏳ KibanaConfigWriterHook eventing model update (temporarily disabled with TODO)
- ⏳ Full testing of Aspire 13 JavaScript integration
- ⏳ Performance profiling to measure .NET 10 improvements

## References
- [.NET 10 What's New](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Aspire 13 What's New](https://aspire.dev/whats-new/aspire-13/)
- [Aspire 13 Breaking Changes](https://learn.microsoft.com/en-us/dotnet/aspire/compatibility/13.0/)
- [C# 14 Language Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
