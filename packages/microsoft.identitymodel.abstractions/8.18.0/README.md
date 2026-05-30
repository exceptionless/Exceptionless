# IdentityModel Extensions for .NET

[![Nuget](https://img.shields.io/nuget/v/Microsoft.IdentityModel.JsonWebTokens?label=Latest%20release)](https://www.nuget.org/packages/Microsoft.IdentityModel.JsonWebTokens/)

The **IdentityModel Extensions for .NET** library provides robust tools to enhance authentication and authorization workflows in your .NET applications. Backed by the Entra team, this library simplifies working with OpenID Connect (OIDC), OAuth2.0, and JSON Web Tokens (JWT) in .NET.

Whether you're building secure APIs, implementing token validation, or managing claims, this library is designed to handle the heavy lifting for you.

> **Why IdentityModel?**
> - **Widely Adopted:** Trusted by thousands of developers to integrate OIDC and OAuth2.0 standards.
> - **Secure by Design:** Built with security as a priority to reduce common vulnerabilities.
> - **Extensible:** Easily extend or customize for advanced use cases.
> - **Battle hardened:** Validates 5+ trillion requests daily, and growing.

## Versions

You can find the release notes for each version [here](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/releases). Older versions can be found [here](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/wiki/Release-Notes).

## Version Lifecycle and Support Matrix

See [Long Term Support policy](./supportPolicy.md) for details.

| Major Version | Currently Supported Version | Status |
| --------------|--------------|--------|
| 8.x           | from 8.0.1 to [![Nuget](https://img.shields.io/nuget/v/Microsoft.IdentityModel.JsonWebTokens?label=Latest%20release)](https://www.nuget.org/packages/Microsoft.IdentityModel.JsonWebTokens/)        | Active (Current) - Tied to .NET 9 (STS) & 10 (LTS) ~ Nov, 2028|
| 7.x           | 7.7.1        | Supported (LTS) through .NET 8 LTS lifetime Nov 10, 2026.<br/>⚠️Versions `< 7.7.1` not supported.|
| 5.x           | 5.7.0        | Supported (LTS), tied to the Microsoft.Owin.Security.JWT 4.2.2 lifetime.<br/>⚠️Versions `< 5.7.0` not supported. |

## IdentityModel 8.x

Version `8.x` introduces significant updates and improvements:
- **Enhanced Performance:** Optimized token validation to handle high-throughput scenarios.
- **.NET Compatibility:** Fully compatible with .NET 9.

>🧭LTS: Supported through .NET 9 LTS lifetime: May 12, 2026 + .NET 10 LTS (~3 years).

## IdentityModel 7.x

[IdentityModel 7x](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/wiki/IdentityModel-7x) introduced several improvements related to serialization and consistency in the API, which provide a better user experience for developers, as well as full AOT compatibility on .NET, and considerable performance improvements compared to IdentityModel 6x.

>🧭LTS: Supported through .NET 8 LTS lifetime: Nov 10, 2026.
>
>⚡Recommendation: Move to 8.x.

## IdentityModel 6.x

>🧭Deprecated: Support ended with .NET 7 LTS lifetime: May 2024.
>
>⚡Action: Move to 8.x.

## IdentityModel 5.x

__Not a recommended version__

>🧭LTS: Supported for Microsoft.Owin.Security.JWT
>
>⚡Action: Move to 8.x.

## Samples and Documentation

The scenarios supported by IdentityModel extensions for .NET are described in [Scenarios](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/wiki/scenarios). The libraries are in particular used part of ASP.NET security to validate tokens in ASP.NET Web Apps and Web APIs. To learn more about token validation, and find samples, see:

- [Microsoft Entra ID with ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/azure-active-directory/?view=aspnetcore-2.1)
- [Developing ASP.NET Apps with Microsoft Entra ID](https://docs.microsoft.com/en-us/aspnet/identity/overview/getting-started/developing-aspnet-apps-with-windows-azure-active-directory)
- [Validating tokens](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/wiki/ValidatingTokens)
- more generally, the library's [Wiki](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/wiki)
- the [reference documentation](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler?view=msal-web-dotnet-latest)

## Community Help and Support

Report a bug or request a feature directly in the [GitHub repo](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/new/choose).

Have a design proposal? Please submit [a design proposal](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/new?assignees=&labels=design-proposal&projects=&template=design_proposal.md) before starting work on a PR to ensure it means the goals/objectives of this library and it's priorities.

We leverage [Stack Overflow](http://stackoverflow.com/) to work with the community on supporting Microsoft Entra and its SDKs, including this one! We highly recommend you ask your questions on Stack Overflow (we're all on there!) Also browse existing issues to see if someone has had your question before.

We recommend you use the "identityModel" tag so we can see it! Here is the latest Q&A on Stack Overflow for IdentityModel: [https://stackoverflow.com/questions/tagged/identityModel](https://stackoverflow.com/questions/tagged/identityModel)

## Security Reporting

See [SECURITY.md](./SECURITY.md)

## Contributing

All code is licensed under the MIT license and we triage actively on GitHub. We enthusiastically welcome contributions and feedback. See [Contributing.md](./Contributing.md) for guidelines, branch information, build instructions, and legalese.

## License

Copyright (c) Microsoft Corporation.  All rights reserved. Licensed under the MIT License (the "License");

## We Value and Adhere to the Microsoft Open Source Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
