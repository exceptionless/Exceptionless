# FluentRest

Lightweight fluent wrapper over HttpClient to make REST calls easier

[![Build status](https://github.com/loresoft/FluentRest/actions/workflows/dotnet.yml/badge.svg)](https://github.com/loresoft/FluentRest/actions)

[![NuGet Version](https://img.shields.io/nuget/v/FluentRest.svg?style=flat-square)](https://www.nuget.org/packages/FluentRest/)

[![Coverage Status](https://coveralls.io/repos/github/loresoft/FluentRest/badge.svg?branch=master)](https://coveralls.io/github/loresoft/FluentRest?branch=master)

## Download

The FluentRest library is available on nuget.org via package name `FluentRest`.

To install FluentRest, run the following command in the Package Manager Console

    PM> Install-Package FluentRest
    
More information about NuGet package available at
<https://nuget.org/packages/FluentRest>

## Development Builds

Development builds are available on the feedz.io feed.  A development build is promoted to the main NuGet feed when it's determined to be stable. 

In your Package Manager settings add the following package source for development builds:
<https://f.feedz.io/loresoft/open/nuget/index.json>

## Features

* Fluent request building
* Fluent form data building
* Automatic deserialization of response content
* Plugin different serialization
* Fake HTTP responses for testing
* Support HttpClientFactory typed client and middleware handlers


## Fluent Request

Create a form post request

```csharp
var client = new HttpClient();
client.BaseAddress = new Uri("http://httpbin.org/", UriKind.Absolute);

var result = await client.PostAsync<EchoResult>(b => b
    .AppendPath("Project")
    .AppendPath("123")
    .FormValue("Test", "Value")
    .FormValue("key", "value")
    .QueryString("page", 10)
);
```

Custom authorization header

```csharp
var client = new HttpClient();
client.BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute);

var result = await client.GetAsync<Repository>(b => b
    .AppendPath("repos")
    .AppendPath("loresoft")
    .AppendPath("FluentRest")
    .Header(h => h.Authorization("token", "7ca..."))
);
```

Use with HttpClientFactory and Retry handler

```csharp
var services = new ServiceCollection();

services.AddSingleton<IContentSerializer, JsonContentSerializer>();
services.AddHttpClient<GithubClient>(c =>
    {
        c.BaseAddress = new Uri("https://api.github.com/");

        c.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        c.DefaultRequestHeaders.Add("User-Agent", "GitHubClient");
    })
    .AddHttpMessageHandler(() => new RetryHandler());

var serviceProvider = services.BuildServiceProvider();

var client = serviceProvider.GetService<GithubClient>();
var result = await client.GetAsync<Repository>(b => b
    .AppendPath("repos")
    .AppendPath("loresoft")
    .AppendPath("FluentRest")
);
```

## Fake Response

`FluentRest.Fake` package adds the ability to fake an HTTP responses by using a custom HttpClientHandler. Faking the HTTP response allows creating unit tests without having to make the actual HTTP call.

To install FluentRest.Fake, run the following command in the Package Manager Console

    PM> Install-Package FluentRest.Fake


### Fake Response Stores

Fake HTTP responses can be stored in the following message stores.  To create your own message store, implement `IFakeMessageStore`.

#### MemoryMessageStore

The memory message store allows composing a JSON response in the unit test.  Register the responses on the start of the unit test.

Register a fake response by URL.

```csharp
MemoryMessageStore.Current.Register(b => b
    .Url("https://api.github.com/repos/loresoft/FluentRest")
    .StatusCode(HttpStatusCode.OK)
    .ReasonPhrase("OK")
    .Content(c => c
        .Header("Content-Type", "application/json; charset=utf-8")
        .Data(responseObject) // object to be JSON serialized
    )
);
```

Use the fake response in a unit test

```csharp
var serializer = new JsonContentSerializer();

// use memory store by default
var fakeHttp = new FakeMessageHandler();

var httpClient = new HttpClient(fakeHttp, true);
httpClient.BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute);

var client = new FluentClient(httpClient, serializer);

// make HTTP call
var result = await client.GetAsync<Repository>(b => b
    .AppendPath("repos")
    .AppendPath("loresoft")
    .AppendPath("FluentRest")
    .Header(h => h.Authorization("token", "7ca..."))
);
```

Use fake handlers with HttpClientFactory

```csharp
var services = new ServiceCollection();

services.AddSingleton<IContentSerializer, JsonContentSerializer>();
services.AddSingleton<IFakeMessageStore>(s => MemoryMessageStore.Current);

services
    .AddHttpClient<EchoClient>(c => c.BaseAddress = new Uri("http://httpbin.org/"))
    .AddHttpMessageHandler(s => new FakeMessageHandler(s.GetService<IFakeMessageStore>(), FakeResponseMode.Fake));

var serviceProvider = services.BuildServiceProvider();

// fake response object
var response = new EchoResult();
response.Url = "http://httpbin.org/post?page=10";
response.Headers["Accept"] = "application/json";
response.QueryString["page"] = "10";
response.Form["Test"] = "Fake";
response.Form["key"] = "value";

// setup fake response
MemoryMessageStore.Current.Register(b => b
    .Url("http://httpbin.org/post?page=10")
    .StatusCode(HttpStatusCode.OK)
    .ReasonPhrase("OK")
    .Content(c => c
        .Header("Content-Type", "application/json; charset=utf-8")
        .Data(response)
    )
);

var client = serviceProvider.GetService<EchoClient>();

var result = await client.PostAsync<EchoResult>(b => b
    .AppendPath("post")
    .FormValue("Test", "Fake")
    .FormValue("key", "value")
    .QueryString("page", 10)
).ConfigureAwait(false);
```

#### FileMessageStore

The file message store allows saving an HTTP call response on the first use.  You can then use that saved response for all future unit test runs.


Configure the FluentRest to capture response.

```csharp
var serializer = new JsonContentSerializer();

// use file store to load from disk
var fakeStore = new FileMessageStore();
fakeStore.StorePath = @".\GitHub\Responses";

var fakeHttp = new FakeMessageHandler(fakeStore, FakeResponseMode.Capture);

var httpClient = new HttpClient(fakeHttp, true);
httpClient.BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute);

var client = new FluentClient(httpClient, serializer);

var result = await client.GetAsync<Repository>(b => b
    .AppendPath("repos")
    .AppendPath("loresoft")
    .AppendPath("FluentRest")
    .Header(h => h.Authorization("token", "7ca..."))
);
```

Use captured response

```csharp
var serializer = new JsonContentSerializer();

// use file store to load from disk
var fakeStore = new FileMessageStore();
fakeStore.StorePath = @".\GitHub\Responses";

var fakeHttp = new FakeMessageHandler(fakeStore, FakeResponseMode.Fake);

var httpClient = new HttpClient(fakeHttp, true);
httpClient.BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute);

var client = new FluentClient(httpClient, serializer);

var result = await client.GetAsync<Repository>(b => b
    .AppendPath("repos")
    .AppendPath("loresoft")
    .AppendPath("FluentRest")
    .Header(h => h.Authorization("token", "7ca..."))
);
```

## Change Log

### Version 6.0

* [Breaking] Remove netstandard1.3 support
* add overload for generic AppendPath
* update dependence packages

### Version 5.0

* [Breaking] Major refactor to support HttpClientFactory
* [Breaking] `FluentClient` changed to a light wrapper for `HttpClient`
* [Breaking] Removed `FluentClient.BaseUri` defaults, use `HttpClient.BaseAddress` instead
* [Breaking] Removed `FluentClient` default headers, use `HttpClient` instead
* [Breaking] All fluent builder take `HttpRequestMessage` instead of `FluentRequest`
* [Breaking] Removed `FluentRequest` and `FluentResponse` classes
* [Breaking] Removed `FluentRequest.Create` fluent builder
* [Breaking] Moved all Fake Response handlers to `FluentRest.Fake` Nuget Package
* [Breaking] Removed interceptor support in favor of HttpClientFactory middleware handlers
* Add support for HttpClientFactory typed client and middleware handlers
* Add `FluentRequest.Factory` to support named FluentClient instances
