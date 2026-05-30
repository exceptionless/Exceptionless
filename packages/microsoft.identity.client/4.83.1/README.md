# Microsoft Authentication Library (MSAL) for .NET

The MSAL library for .NET is part of the [Microsoft identity platform for developers](https://aka.ms/aaddevv2) (formerly named Azure AD) v2.0. It enables you to acquire security tokens to call protected APIs. It uses industry standard OAuth2 and OpenID Connect. The library also supports [Azure AD B2C](https://azure.microsoft.com/services/active-directory-b2c/).

**Quick links:**

| [Conceptual documentation](https://aka.ms/msalnet) | [Getting Started](https://learn.microsoft.com/entra/msal/dotnet/getting-started/choosing-msal-dotnet) | [Sample Code](https://aka.ms/aaddevsamplesv2) | [API Reference](https://learn.microsoft.com/dotnet/api/overview/) | [Support](README.md#community-help-and-support) | [Feedback](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues)
| ------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------- | ------------------------------------------------------------------------------------------------------- |

## NuGet Package

[![NuGet](https://img.shields.io/nuget/v/microsoft.identity.client.svg?style=flat-square&label=nuget&colorB=00b200)](https://www.nuget.org/packages/Microsoft.Identity.Client/)

## Version Lifecycle and Support Matrix

See [Long Term Support policy](./supportPolicy.md) for details.

The following table lists MSAL.NET versions currently supported and receiving security fixes.

| Major Version | Last Release | Patch Release Date  | Support Phase|End of Support |
| --------------|--------------|--------|------------|--------|
| 4.x           | [![NuGet](https://img.shields.io/nuget/v/microsoft.identity.client.svg?style=flat-square&label=nuget&colorB=00b200)](https://www.nuget.org/packages/Microsoft.Identity.Client/)   |Monthly| Active | Not planned.<br/>✅Supported versions: from 4.77.1 to [![NuGet](https://img.shields.io/nuget/v/microsoft.identity.client.svg?style=flat-square&label=nuget&colorB=00b200)](https://www.nuget.org/packages/Microsoft.Identity.Client/)<br/>⚠️Unsupported versions &lt; `4.77.1`.|

### Performance perspectives

[Our documentation](https://learn.microsoft.com/entra/msal/dotnet/advanced/performance-testing) describes the approach to performance testing.

View some of the historical performance benchmark results in [our dashboard](https://azuread.github.io/microsoft-authentication-library-for-dotnet/benchmarks/).

## Support SLA

MSAL.NET became Generally Available with MSAL.NET 3.0.8. Since MSAL.NET moved to version 4:

- Major versions are supported for twelve months after the release of the next major version.
- Minor versions older than N-1 are not supported.

> **Note**
> Minor versions include bug fixes or features with non-breaking (additive) API changes. It is expected that applications using the library can upgrade through the IDE or CLI with no friction. We will not patch old minor versions of the library. When opening new issues, please confirm that you are using the latest minor version.

## Using MSAL.NET

- Guides, tutorials, and detailed walkthroughs are available [on Microsoft Learn](https://learn.microsoft.com/entra/msal/dotnet/getting-started/choosing-msal-dotnet).
- API documentation is available [on Microsoft Learn](https://learn.microsoft.com/dotnet/api/microsoft.identity.client)
- Code samples are available from our [Samples](https://aka.ms/aaddevsamplesv2) page.

## Where do I file issues

You can file new issues in [this repository](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues).

## Community help and support

We use [Stack Overflow](https://stackoverflow.com/questions/tagged/azure-ad-msal) with the community to provide support. We highly recommend you ask your questions on Stack Overflow first and browse [existing issues](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues) to see if someone has asked your question before.

If you find a bug or have a feature request, please raise the issue on [GitHub Issues](../../issues).

## Contribute

We welcome contributions and feedback. You can fork and clone the repo and start contributing now. Read our [Contribution Guide](CONTRIBUTING.md) for more information.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Security library

This library controls how users sign-in and access services. We recommend you always take the latest version of our library in your app when possible. We use [semantic versioning](http://semver.org) so you can control the risk associated with updating your app. As an example, always downloading the latest minor version number (e.g. x.*y*.z) ensures you get the latest security and feature enhancements but our API surface remains the same. You can always see the latest version and release notes under the Releases tab of GitHub.

## Security reporting

If you find a security issue with our libraries or services please report it to https://msrc.microsoft.com/report/vulnerability in as much detail as possible. Your submission may be eligible for a bounty through the [Microsoft Bug Bounty](https://aka.ms/bugbounty) program. Please do not post security issues to GitHub Issues or any other public site. We will contact you shortly after receiving the information. We encourage you to get notifications of when security incidents occur by visiting the [Microsoft Technical Security Notifications page](https://www.microsoft.com/msrc/technical-security-notifications?rtc=1) and subscribing to Security Advisory Alerts.

## Data collection

The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.

See the [our telemetry documentation](https://learn.microsoft.com/entra/msal/dotnet/resources/telemetry-overview) for an example of the telemetry collected by MSAL.NET.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.

Copyright © Microsoft Corporation. All rights reserved. Licensed under the MIT License (the "License").
