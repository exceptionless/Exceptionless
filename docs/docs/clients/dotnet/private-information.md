---
title: "Private Information"
order: 9
---

# Private Information

By default the Exceptionless Client will report all available metadata which could include potentially private data. There are various ways to limit the scope of data collection. For example, one could use [Data Exclusions](/docs/security) to remove sensitive values but it only applies to specific collection points such as `Cookie Keys`, `Form Data Keys`, `Query String Keys` and `Extra Exception properties`. Additional data may need to be removed like the collection of user names and IP Addresses. Shown below is several examples of how you can configure the client to remove this additional data.

You have the option of finely tuning what is collected via individual setting options or you can disable the collection of all private data by setting the `IncludePrivateInformation` to `false`.

## Configuration File

```xml
<exceptionless apiKey="YOUR_API_KEY" includePrivateInformation="false" />
```

## Code

```csharp
ExceptionlessClient.Default.Configuration.IncludePrivateInformation = false;
```

If you wish to have a finer grained approach which allows you to use Data Exclusions while removing specific meta data collection you can do so via code. Please note if the below doesn't meet your needs you can always [write a plugin](/docs/clients/dotnet/plugins).

## Configuration

```csharp
// Include the username if available (E.G., Environment.UserName or IIdentity.Name)
ExceptionlessClient.Default.Configuration.IncludeUserName = false;
// Include the MachineName in MachineInfo.
ExceptionlessClient.Default.Configuration.IncludeMachineName = false;
// Include Ip Addresses in MachineInfo and RequestInfo.
ExceptionlessClient.Default.Configuration.IncludeIpAddress = false;
// Include Cookies, please note that DataExclusions are applied to all Cookie keys when enabled.
ExceptionlessClient.Default.Configuration.IncludeCookies = false;
// Include Form/POST Data, please note that DataExclusions are only applied to Form data keys when enabled.
ExceptionlessClient.Default.Configuration.IncludePostData = false;
// Include Query String information, please note that DataExclusions are applied to all Query String keys when enabled.
ExceptionlessClient.Default.Configuration.IncludeQueryString = false;
```

---

[Next > Troubleshooting](/docs/clients/dotnet/troubleshooting)
