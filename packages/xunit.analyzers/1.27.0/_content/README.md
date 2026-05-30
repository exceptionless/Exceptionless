# About This Project

This project contains source code analysis and cleanup rules for xUnit.net. Analysis and fixes are only supported with C#.

**Requirements**: xUnit.net v2 or v3. Supported in Visual Studio 2019 16.11+ or 2022 17.4+ (as well as via command line builds with Roslyn 3.11+).
Other environments (such as Mono or JetBrains Rider) may be able to use these analyzers as well; support and issue resolution will be provided by
those third parties and not by xUnit.net itself.

**Documentation**: a list of supported rules is available at https://xunit.net/xunit.analyzers/rules/

**Bugs and issues**: please visit the [core xUnit.net project issue tracker](https://github.com/xunit/xunit/issues).

**Building**: see [BUILDING.md](https://github.com/xunit/xunit.analyzers/blob/main/BUILDING.md).

## How to install

- xUnit.net v3: the analyzer package is referenced by the main [`xunit.v3` NuGet package](https://www.nuget.org/packages/xunit.v3) out of the box.
If you choose to reference [`xunit.v3.core`](https://www.nuget.org/packages/xunit.v3.core) instead, you can reference
[`xunit.analyzers`](https://www.nuget.org/packages/xunit.analyzers) explicitly.

- xUnit.net v2 2.3.0 and higher: the analyzer package is referenced by the main [`xunit` NuGet package](https://www.nuget.org/packages/xunit) out of the box.
If you choose to reference [`xunit.core`](https://www.nuget.org/packages/xunit.core) instead, you can reference
[`xunit.analyzers`](https://www.nuget.org/packages/xunit.analyzers) explicitly.

- xUnit.net v2 2.2.0 and earlier: you have to install the [`xunit.analyzers` NuGet package](https://www.nuget.org/packages/xunit.analyzers) explicitly.

## How to uninstall

- If you are using xUnit.net v3 and do not wish to use the analyzers package, replace the package reference
to [`xunit.v3`](https://www.nuget.org/packages/xunit.v3) with the corresponding versions of [`xunit.v3.core`](https://www.nuget.org/packages/xunit.v3.core)
and [`xunit.v3.assert`](https://www.nuget.org/packages/xunit.v3.assert).

- If you are using xUnit.net v2 2.3.0 or higher and do not wish to use the analyzers package, replace the package reference
to [`xunit`](https://www.nuget.org/packages/xunit) with the corresponding versions of [`xunit.core`](https://www.nuget.org/packages/xunit.core)
and [`xunit.assert`](https://www.nuget.org/packages/xunit.assert).

- If you are using xUnit.net v2 v2.2.0 or earlier: remove the reference to the [`xunit.analyzers` NuGet package](https://www.nuget.org/packages/xunit.analyzers).

## Analysis and Code Fix in Action

![Analyzer in action animation](https://cloud.githubusercontent.com/assets/607223/25752060/fb4af444-316b-11e7-9e7c-fc69ade132fb.gif)

# About xUnit.net

xUnit.net is a free, open source, community-focused unit testing tool for C#, F#, and Visual Basic.

xUnit.net works with the [.NET SDK](https://dotnet.microsoft.com/download) command line tools, [Visual Studio](https://visualstudio.microsoft.com/), [Visual Studio Code](https://code.visualstudio.com/), [JetBrains Rider](https://www.jetbrains.com/rider/), [NCrunch](https://www.ncrunch.net/), and any development environment compatible with [Microsoft Testing Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro) (xUnit.net v3) or [VSTest](https://github.com/microsoft/vstest) (all versions of xUnit.net).

xUnit.net is part of the [.NET Foundation](https://www.dotnetfoundation.org/) and operates under their [code of conduct](https://www.dotnetfoundation.org/code-of-conduct). It is licensed under [Apache 2](https://opensource.org/licenses/Apache-2.0) (an OSI approved license). The project is [governed](https://xunit.net/governance) by a Project Lead.

For project documentation, please visit the [xUnit.net project home](https://xunit.net/).

* _New to xUnit.net? Get started with the [.NET SDK](https://xunit.net/docs/getting-started/v3/getting-started)._
* _Need some help building the source? See [BUILDING.md](https://github.com/xunit/xunit/tree/main/BUILDING.md)._
* _Want to contribute to the project? See [CONTRIBUTING.md](https://github.com/xunit/.github/tree/main/CONTRIBUTING.md)._
* _Want to contribute to the assertion library? See the [suggested contribution workflow](https://github.com/xunit/assert.xunit/tree/main/README.md#suggested-contribution-workflow) in the assertion library project, as it is slightly more complex due to code being spread across two GitHub repositories._

## Latest Builds

|                             | Latest stable                                                                                                                            | Latest CI ([how to use](https://xunit.net/docs/using-ci-builds))                                                                                                                                                                          | Build status
| --------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------
| `xunit.v3`                  | [![](https://img.shields.io/nuget/v/xunit.v3.svg?logo=nuget)](https://www.nuget.org/packages/xunit.v3)                                   | [![](https://img.shields.io/badge/endpoint.svg?url=https://f.feedz.io/xunit/xunit/shield/xunit.v3/latest&color=f58142)](https://feedz.io/org/xunit/repository/xunit/packages/xunit.v3)                                   | [![](https://img.shields.io/endpoint.svg?url=https://actions-badge.atrox.dev/xunit/xunit/badge%3Fref%3Dmain&amp;label=build)](https://actions-badge.atrox.dev/xunit/xunit/goto?ref=main)
| `xunit`                     | [![](https://img.shields.io/nuget/v/xunit.svg?logo=nuget)](https://www.nuget.org/packages/xunit)                                         | [![](https://img.shields.io/badge/endpoint.svg?url=https://f.feedz.io/xunit/xunit/shield/xunit/latest&color=f58142)](https://feedz.io/org/xunit/repository/xunit/packages/xunit)                                         | [![](https://img.shields.io/endpoint.svg?url=https://actions-badge.atrox.dev/xunit/xunit/badge%3Fref%3Dv2&amp;label=build)](https://actions-badge.atrox.dev/xunit/xunit/goto?ref=v2)
| `xunit.analyzers`           | [![](https://img.shields.io/nuget/v/xunit.analyzers.svg?logo=nuget)](https://www.nuget.org/packages/xunit.analyzers)                     | [![](https://img.shields.io/badge/endpoint.svg?url=https://f.feedz.io/xunit/xunit/shield/xunit.analyzers/latest&color=f58142)](https://feedz.io/org/xunit/repository/xunit/packages/xunit.analyzers)                     | [![](https://img.shields.io/endpoint.svg?url=https://actions-badge.atrox.dev/xunit/xunit.analyzers/badge%3Fref%3Dmain&amp;label=build)](https://actions-badge.atrox.dev/xunit/xunit.analyzers/goto?ref=main)
| `xunit.runner.visualstudio` | [![](https://img.shields.io/nuget/v/xunit.runner.visualstudio.svg?logo=nuget)](https://www.nuget.org/packages/xunit.runner.visualstudio) | [![](https://img.shields.io/badge/endpoint.svg?url=https://f.feedz.io/xunit/xunit/shield/xunit.runner.visualstudio/latest&color=f58142)](https://feedz.io/org/xunit/repository/xunit/packages/xunit.runner.visualstudio) | [![](https://img.shields.io/endpoint.svg?url=https://actions-badge.atrox.dev/xunit/visualstudio.xunit/badge%3Fref%3Dmain&amp;label=build)](https://actions-badge.atrox.dev/xunit/visualstudio.xunit/goto?ref=main)

*For complete CI package lists, please visit the [feedz.io package search](https://feedz.io/org/xunit/repository/xunit/search). A free login is required.*

## Sponsors

Help support this project by becoming a sponsor through [GitHub Sponsors](https://github.com/sponsors/xunit).
