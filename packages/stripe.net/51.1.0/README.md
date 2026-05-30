# Stripe.net

[![NuGet](https://img.shields.io/nuget/v/stripe.net.svg)](https://www.nuget.org/packages/Stripe.net/)
[![Build Status](https://github.com/stripe/stripe-dotnet/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/stripe/stripe-dotnet/actions?query=branch%3Amaster)
[![Coverage Status](https://coveralls.io/repos/github/stripe/stripe-dotnet/badge.svg?branch=master)](https://coveralls.io/github/stripe/stripe-dotnet?branch=master)

> [!TIP]
> Want to chat live with Stripe engineers? Join us on our [Discord server](https://stripe.com/go/discord/dotnet).

The official [Stripe][stripe] .NET library, supporting .NET Standard 2.0+, .NET Core 5+, and .NET Framework 4.6.2+.

## Installation

Using the [.NET Core command-line interface (CLI) tools][dotnet-core-cli-tools]:

```sh
dotnet add package Stripe.net
```

Using the [NuGet Command Line Interface (CLI)][nuget-cli]:

```sh
nuget install Stripe.net
```

Using the [Package Manager Console][package-manager-console]:

```powershell
Install-Package Stripe.net
```

From within Visual Studio:

1. Open the Solution Explorer.
2. Right-click on a project within your solution.
3. Click on _Manage NuGet Packages..._
4. Click on the _Browse_ tab and search for "Stripe.net".
5. Click on the Stripe.net package, select the appropriate version in the
   right-tab and click _Install_.

### Requirements

Per our [Language Version Support Policy](https://docs.stripe.com/sdks/versioning?lang=dotnet#stripe-sdk-language-version-support-policy), we currently support **.NET Standard 2.0+, LTS versions of .NET Core 6+, and .NET Framework 4.6.2+.**.

Support for version 6 will be removed in a future version. Read more and see the full schedule in the docs: https://docs.stripe.com/sdks/versioning?lang=dotnet#stripe-sdk-language-version-support-policy

## Documentation

For a comprehensive list of examples, check out the [API
documentation][api-docs]. See [video demonstrations][youtube-playlist] covering
how to use the library.

## Usage

### Using StripeClient

In version 46 of the Stripe .NET SDK, we have enhanced the `StripeClient` class to be the entry point to access all services that had to be previously independently instantiated with global configuration. This improves discoverability during IDE auto-completion and creates a more intuitive developer experience for you.

Each client instantiation can have its own configuration so you can access Stripe API with different API keys or different configuration (like number of retries) on a per client basis and without changing a global configuration.

```C#
// StripeClient pattern (Recommended)
var client = new StripeClient("sk_test_...");
Customer customer = client.V1.Customers.Get("cus_1234");

// Global Configuration pattern (Legacy)
StripeConfiguration.ApiKey = "sk_test_...";
var service = new CustomerService();
Customer customer = service.Get("cus_1234");
```

The previous global configuration pattern will continue to be supported.

### Authentication

Stripe authenticates API requests using your account’s secret key, which you can find in the Stripe Dashboard. By default, secret keys can be used to perform any API request without restriction.

```C#
var client = new StripeClient("sk_test_...");
```

### Creating a resource

The `Create` method of the service class can be used to create a new resource:

```C#
var options = new CustomerCreateOptions
{
    Email = "customer@example.com"
};

var client = new StripeClient("sk_test_...");
Customer customer = client.V1.Customers.Create(options);

// Newly created customer is returned
Console.WriteLine(customer.Email);
```

### Retrieve a resource

The `Retrieve` method of the service class can be used to retrieve a resource:

```C#
var client = new StripeClient("sk_test_...");
Customer customer = client.V1.Customers.Get("cus_1234");

Console.WriteLine(customer.Email);
```

### Updating a resource

The `Update` method of the service class can be used to update a resource:

```C#
var options = new CustomerUpdateOptions
{
    Email = "updated-email@example.com"
};

var client = new StripeClient("sk_test_...");
Customer customer = client.V1.Customers.Update("cus_123", options);

// The updated customer is returned
Console.WriteLine(customer.Email);
```

### Deleting a resource

The `Delete` method of the service class can be used to delete a resource:

```C#
var client = new StripeClient("sk_test_...");
Customer customer = client.V1.Customers.Delete("cus_123", options);
```

### Listing a resource

The `List` method on the service class can be used to list resources page-by-page.

> **NOTE**
> The `List` method returns only a single page, you have to manually continue the iteration using the `StartingAfter` parameter.

```C#
var client = new StripeClient("sk_test_...");
var customers = client.V1.Customers.List();

string lastId = null;

// Enumerate the first page of the list
foreach (Customer customer in customers)
{
   lastId = customer.Id;
   Console.WriteLine(customer.Email);
}

customers = service.List(new CustomerListOptions()
{
    StartingAfter = lastId,
});

// Enumerate the subsequent page
foreach (Customer customer in customers)
{
   lastId = customer.Id;
   Console.WriteLine(customer.Email);
}
```

### Listing a resource with auto-pagination

The `ListAutoPaging` method on the service class can be used to automatically iterate over all pages.

```C#
var client = new StripeClient("sk_test_...");
var customers = client.V1.Customers.ListAutoPaging();

// Enumerate all pages of the list
foreach (Customer customer in customers)
{
   Console.WriteLine(customer.Email);
}
```

### Per-request configuration

All the service methods accept an optional `RequestOptions` object. This is
used if you want to set an [idempotency key][idempotency-keys], if you are
using [Stripe Connect][connect-auth], or if you want to pass the secret API
key on each method.

```c#
var requestOptions = new RequestOptions();
requestOptions.ApiKey = "SECRET API KEY";
requestOptions.IdempotencyKey = "SOME STRING";
requestOptions.StripeAccount = "CONNECTED ACCOUNT ID";
```

### Using a custom `HttpClient`

You can configure the library with your own custom `HttpClient`:

```c#
StripeConfiguration.StripeClient = new StripeClient(
    apiKey,
    httpClient: new SystemNetHttpClient(httpClient));
```

Please refer to the [Advanced client usage][advanced-client-usage] wiki page
to see more examples of using custom clients, e.g. for using a proxy server, a
custom message handler, etc.

### Automatic retries

The library automatically retries requests on intermittent failures like on a
connection error, timeout, or on certain API responses like a status `409
Conflict`. [Idempotency keys][idempotency-keys] are always added to requests to
make any such subsequent retries safe.

By default, it will perform up to two retries. That number can be configured
with `StripeConfiguration.MaxNetworkRetries`:

```c#
StripeConfiguration.MaxNetworkRetries = 0; // Zero retries
```

### How to use undocumented parameters and properties

stripe-dotnet is a typed library and it supports all public properties or parameters.

Stripe sometimes has beta features which introduce new properties or parameters that are not immediately public. The library does not support these properties or parameters until they are public but there is still an approach that allows you to use them.

#### Parameters

To pass undocumented parameters to Stripe using stripe-dotnet you need to use the `AddExtraParam()` method, as shown below:

```c#
var options = new CustomerCreateOptions
{
    Email = "jenny.rosen@example.com"
}
options.AddExtraParam("secret_feature_enabled", "true");
options.AddExtraParam("secret_parameter[primary]", "primary value");
options.AddExtraParam("secret_parameter[secondary]", "secondary value");

var client = new StripeClient("sk_test_...");
var customer = client.V1.Customers.Create(options);
```

#### Properties

To retrieve undocumented properties from Stripe using C# you can use an option in the library to return the raw JSON object and return the property. An example of this is shown below:

```c#
var client = new StripeClient("sk_test_...");
var customer = client.V1.Customers.Get("cus_1234");

customer.RawJObject["secret_feature_enabled"];
customer.RawJObject["secret_parameter"]["primary"];
customer.RawJObject["secret_parameter"]["secondary"];
```

### Writing a plugin

If you're writing a plugin that uses the library, we'd appreciate it if you
identified using `StripeConfiguration.AppInfo`:

```c#
StripeConfiguration.AppInfo = new AppInfo
{
    Name = "MyAwesomePlugin",
    Url = "https://myawesomeplugin.info",
    Version = "1.2.34",
};
```

This information is passed along when the library makes calls to the Stripe
API. Note that while `Name` is always required, `Url` and `Version` are
optional.

### Telemetry

By default, the library sends telemetry to Stripe regarding request latency and feature usage. These
numbers help Stripe improve the overall latency of its API for all users, and
improve popular features.

You can disable this behavior if you prefer:

```c#
StripeConfiguration.EnableTelemetry = false;
```

### Public Preview SDKs

Stripe has features in the [public preview phase](https://docs.stripe.com/release-phases) that can be accessed via versions of this package that have the `-beta.X` suffix like `45.1.0-beta.2`.
We would love for you to try these as we incrementally release new features and improve them based on your feedback.

To install, pick the latest version with the `beta` suffix by reviewing the [releases page](https://github.com/stripe/stripe-dotnet/releases/) and then use it in the version parameter with `dotnet add package` command:

```
dotnet add package Stripe.net --version <replace-with-the-version-of-your-choice>
```

> **Note**
> There can be breaking changes between two versions of the public preview SDKs without a bump in the major version. Therefore we recommend pinning the package version to a specific version in your project file. This way you can install the same version each time without breaking changes unless you are intentionally looking for the latest public preview SDK.

Some preview features require a name and version to be set in the `Stripe-Version` header like `feature_beta=v3`. If your preview feature has this requirement, use the `StripeConfiguration.AddBetaVersion` function (available only in the public preview SDKs):

```csharp
StripeConfiguration.AddBetaVersion("feature_beta", "v3");
```

### Private Preview SDKs

> This feature is only available from version 46 of this SDK.

Stripe has features in the [private preview phase](https://docs.stripe.com/release-phases) that can be accessed via versions of this package that have the `-alpha.X` suffix like `45.2.0-alpha.2`. These are invite-only features. Once invited, you can install the private preview SDKs by following the same instructions as for the [public preview SDKs](https://github.com/stripe/stripe-dotnet?tab=readme-ov-file#public-preview-sdks) above and replacing the term `beta` with `alpha`.

### Custom requests

If you would like to send a request to an undocumented API (for example you are in a private beta), or if you prefer to bypass the method definitions in the library and specify your request details directly, you can use the `RawRequestAsync` method on `StripeClient`.

```C#
StripeClient client = new StripeClient();
StripeResponse response = await client.RawRequestAsync(HttpMethod.Get, "/v1/accounts/acc_123");

// Optionally use Deserialize to convert the response to strongly-typed object.
Account account = client.Deserialize<Account>(response.Content)
```

## Support

New features and bug fixes are released on the latest major version of the Stripe .NET client library. If you are on an older major version, we recommend that you upgrade to the latest in order to use the new features and bug fixes including those for security vulnerabilities. Older major versions of the package will continue to be available for use, but will not be receiving any updates.

## Development

[Contribution guidelines for this project](CONTRIBUTING.md)

.NET 8 is required to build and test Stripe.net SDK, you can install it from [get.dot.net](https://get.dot.net/).

The test suite depends on [stripe-mock][stripe-mock], so make sure to fetch
and run it from a background terminal
([stripe-mock's README][stripe-mock-usage] also contains instructions for
installing via Homebrew and other methods):

```sh
go install github.com/stripe/stripe-mock@latest
stripe-mock
```

Lastly, we use [just](https://github.com/casey/just) for running common development tasks. You can also read the `justfile` and run those commands directly.

Run all tests from the `src/StripeTests` directory:

```sh
just test
# or: dotnet test src
```

Run some tests, filtering by name:

```sh
dotnet test src --filter FullyQualifiedName~InvoiceServiceTest
```

Run tests for a single target framework:

```sh
dotnet test src --framework net8.0
```

The library uses [`dotnet-format`][dotnet-format] for code formatting. Code
must be formatted before PRs are submitted, otherwise CI will fail. Run the
formatter with:

```sh
just format
# or: dotnet format src/Stripe.net.sln
```

For any requests, bug or comments, please [open an issue][issues] or [submit a
pull request][pulls].

[advanced-client-usage]: https://github.com/stripe/stripe-dotnet/wiki/Advanced-client-usage
[api-docs]: https://stripe.com/docs/api?lang=dotnet
[api-keys]: https://dashboard.stripe.com/apikeys
[connect-auth]: https://stripe.com/docs/connect/authentication#authentication-via-the-stripe-account-header
[dotnet-core-cli-tools]: https://docs.microsoft.com/en-us/dotnet/core/tools/
[dotnet-format]: https://github.com/dotnet/format
[idempotency-keys]: https://stripe.com/docs/api/idempotent_requests?lang=dotnet
[issues]: https://github.com/stripe/stripe-dotnet/issues/new
[nuget-cli]: https://docs.microsoft.com/en-us/nuget/tools/nuget-exe-cli-reference
[package-manager-console]: https://docs.microsoft.com/en-us/nuget/tools/package-manager-console
[pulls]: https://github.com/stripe/stripe-dotnet/pulls
[stripe]: https://stripe.com
[stripe-mock]: https://github.com/stripe/stripe-mock
[stripe-mock-usage]: https://github.com/stripe/stripe-mock#usage
[youtube-playlist]: https://www.youtube.com/playlist?list=PLy1nL-pvL2M4cNNoUtjWevYSci4ubsbhC
