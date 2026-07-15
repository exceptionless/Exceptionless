using System.Net;
using System.Text;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventProcessor.Default;
using Exceptionless.Core.Services.SourceMaps;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests.Services.SourceMaps;

public sealed class SourceMapServiceTests : TestWithServices
{
    private const string ProjectId = "507f1f77bcf86cd799439011";
    private const string GeneratedFileUrl = "https://cdn.example.com/assets/app.min.js";
    private static readonly byte[] SourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/app.ts"],"names":["meaningfulFunction"],"mappings":"AAAAA"}""");
    private static readonly byte[] UpdatedSourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/updated.ts"],"names":["updatedFunction"],"mappings":"AAAAA"}""");

    public SourceMapServiceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task SymbolicateAsync_WithUploadedSourceMap_RewritesFrameAndPreservesGeneratedLocation()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        var artifact = await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        var error = CreateError();

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.True(changed);
        var frame = Assert.Single(error.StackTrace!);
        Assert.Equal("meaningfulFunction", frame.Name);
        Assert.Equal("src/app.ts", frame.FileName);
        Assert.Equal(1, frame.LineNumber);
        Assert.Equal(1, frame.Column);
        var generatedLocation = Assert.IsType<DataDictionary>(frame.Data!["@source_map"]);
        Assert.Equal(GeneratedFileUrl, generatedLocation["generated_file_name"]);
        Assert.Equal(artifact.Id, generatedLocation["source_map_id"]);
    }

    [Fact]
    public async Task GetArtifactsAsync_AfterUploadAndDelete_ReflectsStoredArtifact()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        var uploaded = await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        var artifacts = await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken);
        bool deleted = await service.DeleteArtifactAsync(ProjectId, uploaded.Id, TestContext.Current.CancellationToken);

        var artifact = Assert.Single(artifacts);
        Assert.Equal(GeneratedFileUrl, artifact.GeneratedFileUrl);
        Assert.False(artifact.IsAutoDownloaded);
        Assert.True(deleted);
        Assert.Empty(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SymbolicateAsync_WithPublicSourceMapHeader_DownloadsAndStoresSourceMap()
    {
        var requestedUris = new List<Uri>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Headers.TryAddWithoutValidation("SourceMap", "app.min.js.map");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        var service = new SourceMapService(
            new TestHttpClientFactory(httpClient),
            GetService<IFileStorage>(),
            GetService<ICacheClient>(),
            GetService<ILockProvider>(),
            GetService<SourceMapRequestThrottle>(),
            GetService<JsonSerializerOptions>(),
            GetService<AppOptions>(),
            GetService<TimeProvider>(),
            GetService<ILogger<SourceMapService>>());
        var error = CreateError();

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal([new Uri(GeneratedFileUrl), new Uri("https://cdn.example.com/assets/app.min.js.map")], requestedUris);
        var artifact = Assert.Single(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.True(artifact.IsAutoDownloaded);
        Assert.Equal("https://cdn.example.com/assets/app.min.js.map", artifact.SourceMapUrl);
    }

    [Fact]
    public async Task SymbolicateAsync_WithExpiredAutoDownloadedSourceMap_RefreshesStoredMap()
    {
        int sourceMapDownloadCount = 0;
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Headers.TryAddWithoutValidation("SourceMap", "app.min.js.map");
                return response;
            }

            sourceMapDownloadCount++;
            byte[] content = sourceMapDownloadCount == 1 ? SourceMap : UpdatedSourceMap;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        });
        using var httpClient = new HttpClient(handler);
        using (var initialService = CreateService(httpClient))
        {
            var initialError = CreateError();
            Assert.True(await initialService.SymbolicateAsync(ProjectId, initialError, TestContext.Current.CancellationToken));
            Assert.Equal("meaningfulFunction", Assert.Single(initialError.StackTrace!).Name);
        }

        TimeProvider.Advance(TimeSpan.FromMinutes(61));

        using var refreshedService = CreateService(httpClient);
        var refreshedError = CreateError();
        Assert.True(await refreshedService.SymbolicateAsync(ProjectId, refreshedError, TestContext.Current.CancellationToken));
        var refreshedFrame = Assert.Single(refreshedError.StackTrace!);
        Assert.Equal("updatedFunction", refreshedFrame.Name);
        Assert.Equal("src/updated.ts", refreshedFrame.FileName);
        Assert.Equal(2, sourceMapDownloadCount);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenExpiredSourceMapCannotRefresh_DoesNotUseStaleMap()
    {
        int sourceMapDownloadCount = 0;
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Headers.TryAddWithoutValidation("SourceMap", "app.min.js.map");
                return response;
            }

            sourceMapDownloadCount++;
            return sourceMapDownloadCount == 1
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) }
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        using var httpClient = new HttpClient(handler);
        using (var initialService = CreateService(httpClient))
            Assert.True(await initialService.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));

        TimeProvider.Advance(TimeSpan.FromMinutes(61));

        using var refreshedService = CreateService(httpClient);
        var error = CreateError();
        Assert.False(await refreshedService.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken));
        var frame = Assert.Single(error.StackTrace!);
        Assert.Equal("a", frame.Name);
        Assert.Equal(GeneratedFileUrl, frame.FileName);
        Assert.Equal(2, sourceMapDownloadCount);
    }

    [Fact]
    public async Task SaveUploadedAsync_WhenReplacingMap_UsesNewMapAndRemovesOldContent()
    {
        var service = GetService<SourceMapService>();
        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        await using (var sourceMapStream = new MemoryStream(UpdatedSourceMap))
            await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        var error = CreateError();
        Assert.True(await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken));
        Assert.Equal("updatedFunction", Assert.Single(error.StackTrace!).Name);

        var mapFiles = await GetService<IFileStorage>().GetFileListAsync($"source-maps/{ProjectId}/*.map", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(mapFiles);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenMapExceedsParsedCacheLimit_DoesNotRetainMapInMemory()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumParsedSourceMapCacheSize = 1;
        int requestCount = 0;
        var handler = new DelegateHandler(request =>
        {
            requestCount++;
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Headers.TryAddWithoutValidation("SourceMap", "app.min.js.map");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        Assert.True(await service.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));

        await GetService<IFileStorage>().DeleteFilesAsync($"source-maps/{ProjectId}/*", TestContext.Current.CancellationToken);

        Assert.True(await service.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));
        Assert.Equal(4, requestCount);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenFirstCallerCancels_KeepsSharedDownloadForSecondCaller()
    {
        int requestCount = 0;
        var mapRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMapRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new AsyncDelegateHandler(async request =>
        {
            requestCount++;
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Headers.TryAddWithoutValidation("SourceMap", "app.min.js.map");
                return response;
            }

            mapRequestStarted.TrySetResult();
            await releaseMapRequest.Task;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        Task<bool> canceledCaller = service.SymbolicateAsync(ProjectId, CreateError(), cancellationTokenSource.Token);
        await mapRequestStarted.Task;

        cancellationTokenSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledCaller);
        Task<bool> secondCaller = service.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken);
        releaseMapRequest.TrySetResult();

        Assert.True(await secondCaller);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task SaveUploadedAsync_DuringAutomaticDownload_UploadRemainsAuthoritative()
    {
        var mapRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMapRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new AsyncDelegateHandler(async request =>
        {
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Headers.TryAddWithoutValidation("SourceMap", "app.min.js.map");
                return response;
            }

            mapRequestStarted.TrySetResult();
            await releaseMapRequest.Task;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        Task<bool> automaticDownload = service.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken);
        await mapRequestStarted.Task;

        await using var uploadedStream = new MemoryStream(UpdatedSourceMap);
        Task<SourceMapArtifact> upload = service.SaveUploadedAsync(
            ProjectId,
            GeneratedFileUrl,
            "app.min.js.map",
            uploadedStream,
            TestContext.Current.CancellationToken);
        releaseMapRequest.TrySetResult();

        Assert.True(await automaticDownload);
        var uploaded = await upload;
        Assert.False(uploaded.IsAutoDownloaded);
        var error = CreateError();
        Assert.True(await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken));
        Assert.Equal("updatedFunction", Assert.Single(error.StackTrace!).Name);
        Assert.False(Assert.Single(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken)).IsAutoDownloaded);
    }

    [Fact]
    public async Task SymbolicateAsync_FreeClientKeyExceedsDiscoveryLimit_StopsDownloadingNewUrls()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumAutoDiscoveriesPerFreeClientKey = 1;
        options.SourceMapOptions.MaximumAutoDiscoveriesPerFreeProject = 10;
        options.SourceMapOptions.MaximumAutoDiscoveriesPerFreeOrganization = 10;
        int requestCount = 0;
        var handler = new DelegateHandler(request =>
        {
            requestCount++;
            if (!request.RequestUri!.AbsolutePath.EndsWith(".map", StringComparison.Ordinal))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Headers.TryAddWithoutValidation("SourceMap", request.RequestUri.AbsolutePath + ".map");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        string suffix = Guid.NewGuid().ToString("N");
        var request = new SourceMapRequest($"organization-{suffix}", $"project-{suffix}", $"key-{suffix}", true);

        Assert.True(await service.SymbolicateAsync(request, CreateError($"https://cdn.example.com/{suffix}/first.js"), TestContext.Current.CancellationToken));
        Assert.False(await service.SymbolicateAsync(request, CreateError($"https://cdn.example.com/{suffix}/second.js"), TestContext.Current.CancellationToken));
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task TryReserveDiscoveryAsync_MultipleClientKeysExceedProjectLimit_BlocksProject()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumAutoDiscoveriesPerClientKey = 10;
        options.SourceMapOptions.MaximumAutoDiscoveriesPerProject = 2;
        options.SourceMapOptions.MaximumAutoDiscoveriesPerOrganization = 10;
        var throttle = GetService<SourceMapRequestThrottle>();
        string suffix = Guid.NewGuid().ToString("N");

        Assert.True(await throttle.TryReserveDiscoveryAsync(new SourceMapRequest($"organization-{suffix}", $"project-{suffix}", $"key-1-{suffix}", false)));
        Assert.True(await throttle.TryReserveDiscoveryAsync(new SourceMapRequest($"organization-{suffix}", $"project-{suffix}", $"key-2-{suffix}", false)));
        Assert.False(await throttle.TryReserveDiscoveryAsync(new SourceMapRequest($"organization-{suffix}", $"project-{suffix}", $"key-3-{suffix}", false)));
    }

    [Fact]
    public async Task TryReserveDiscoveryAsync_SameClientKeyAcrossProjects_UsesSeparateKeyBudgets()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumAutoDiscoveriesPerClientKey = 1;
        options.SourceMapOptions.MaximumAutoDiscoveriesPerProject = 10;
        options.SourceMapOptions.MaximumAutoDiscoveriesPerOrganization = 10;
        var throttle = GetService<SourceMapRequestThrottle>();
        string suffix = Guid.NewGuid().ToString("N");
        string organizationId = $"organization-{suffix}";
        string clientKeyHash = $"key-{suffix}";

        Assert.True(await throttle.TryReserveDiscoveryAsync(new SourceMapRequest(organizationId, $"project-1-{suffix}", clientKeyHash, false)));
        Assert.False(await throttle.TryReserveDiscoveryAsync(new SourceMapRequest(organizationId, $"project-1-{suffix}", clientKeyHash, false)));
        Assert.True(await throttle.TryReserveDiscoveryAsync(new SourceMapRequest(organizationId, $"project-2-{suffix}", clientKeyHash, false)));
    }

    [Fact]
    public async Task TryReserveOutboundRequestAsync_DestinationExceedsLimit_BlocksDestinationOnly()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumAutoDownloadRequestsPerDestination = 1;
        options.SourceMapOptions.MaximumAutoDownloadRequestsGlobally = 10;
        var throttle = GetService<SourceMapRequestThrottle>();
        string suffix = Guid.NewGuid().ToString("N");

        Assert.True(await throttle.TryReserveOutboundRequestAsync(new Uri($"https://{suffix}.example.com/first.js")));
        Assert.False(await throttle.TryReserveOutboundRequestAsync(new Uri($"https://{suffix}.example.com/second.js")));
        Assert.True(await throttle.TryReserveOutboundRequestAsync(new Uri($"https://other-{suffix}.example.com/app.js")));
    }

    [Fact]
    public async Task TryReserveOutboundRequestAsync_RefreshUsesSeparateBudget()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumAutoDownloadRequestsPerDestination = 1;
        options.SourceMapOptions.MaximumAutoDownloadRequestsGlobally = 10;
        options.SourceMapOptions.MaximumAutoRefreshRequestsPerDestination = 1;
        options.SourceMapOptions.MaximumAutoRefreshRequestsGlobally = 10;
        var throttle = GetService<SourceMapRequestThrottle>();
        string suffix = Guid.NewGuid().ToString("N");
        var uri = new Uri($"https://{suffix}.example.com/app.js");

        Assert.True(await throttle.TryReserveOutboundRequestAsync(uri));
        Assert.False(await throttle.TryReserveOutboundRequestAsync(uri));
        Assert.True(await throttle.TryReserveOutboundRequestAsync(uri, isRefresh: true));
        Assert.False(await throttle.TryReserveOutboundRequestAsync(uri, isRefresh: true));
    }

    [Fact]
    public async Task SymbolicateAsync_RepeatedFailedUrl_UsesFailureCacheWithoutAnotherRequest()
    {
        int requestCount = 0;
        var handler = new DelegateHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        string suffix = Guid.NewGuid().ToString("N");
        string generatedFileUrl = $"https://cdn.example.com/{suffix}/missing.js";

        Assert.False(await service.SymbolicateAsync($"project-{suffix}", CreateError(generatedFileUrl), TestContext.Current.CancellationToken));
        Assert.False(await service.SymbolicateAsync($"project-{suffix}", CreateError(generatedFileUrl), TestContext.Current.CancellationToken));
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public void ReadFromConfiguration_WithUnsafeLimits_NormalizesLimits()
    {
        var values = new Dictionary<string, string?>
        {
            ["SourceMaps:RequestTimeoutMilliseconds"] = "0",
            ["SourceMaps:MaximumGeneratedFileSize"] = "0",
            ["SourceMaps:MaximumSourceMapSize"] = "-1",
            ["SourceMaps:MaximumMappingSegments"] = "0",
            ["SourceMaps:MaximumRedirects"] = "-1",
            ["SourceMaps:MaximumConcurrentDownloads"] = "0",
            ["SourceMaps:MaximumConcurrentDownloadsGlobally"] = "0",
            ["SourceMaps:AutoDownloadRateLimitPeriodMinutes"] = "0",
            ["SourceMaps:MaximumAutoDiscoveriesPerFreeClientKey"] = "-1",
            ["SourceMaps:MaximumAutoDiscoveriesPerClientKey"] = "-1",
            ["SourceMaps:MaximumAutoDiscoveriesPerFreeProject"] = "-1",
            ["SourceMaps:MaximumAutoDiscoveriesPerProject"] = "-1",
            ["SourceMaps:MaximumAutoDiscoveriesPerFreeOrganization"] = "-1",
            ["SourceMaps:MaximumAutoDiscoveriesPerOrganization"] = "-1",
            ["SourceMaps:MaximumAutoDownloadRequestsPerDestination"] = "-1",
            ["SourceMaps:MaximumAutoDownloadConnectionsPerIpAddress"] = "-1",
            ["SourceMaps:MaximumAutoDownloadRequestsGlobally"] = "-1",
            ["SourceMaps:MaximumAutoRefreshRequestsPerDestination"] = "-1",
            ["SourceMaps:MaximumAutoRefreshRequestsGlobally"] = "-1",
            ["SourceMaps:MaximumFramesPerError"] = "0",
            ["SourceMaps:MaximumProcessingTimeMilliseconds"] = "0",
            ["SourceMaps:AutoDownloadRefreshIntervalMinutes"] = "0",
            ["SourceMaps:ParsedSourceMapCacheLifetimeMinutes"] = "0",
            ["SourceMaps:MaximumParsedSourceMapCacheSize"] = "0"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var options = SourceMapOptions.ReadFromConfiguration(configuration);

        Assert.Equal(1, options.RequestTimeoutMilliseconds);
        Assert.Equal(1, options.MaximumGeneratedFileSize);
        Assert.Equal(1, options.MaximumSourceMapSize);
        Assert.Equal(1, options.MaximumMappingSegments);
        Assert.Equal(0, options.MaximumRedirects);
        Assert.Equal(1, options.MaximumConcurrentDownloads);
        Assert.Equal(1, options.MaximumConcurrentDownloadsGlobally);
        Assert.Equal(1, options.AutoDownloadRateLimitPeriodMinutes);
        Assert.Equal(0, options.MaximumAutoDiscoveriesPerFreeClientKey);
        Assert.Equal(0, options.MaximumAutoDiscoveriesPerClientKey);
        Assert.Equal(0, options.MaximumAutoDiscoveriesPerFreeProject);
        Assert.Equal(0, options.MaximumAutoDiscoveriesPerProject);
        Assert.Equal(0, options.MaximumAutoDiscoveriesPerFreeOrganization);
        Assert.Equal(0, options.MaximumAutoDiscoveriesPerOrganization);
        Assert.Equal(0, options.MaximumAutoDownloadRequestsPerDestination);
        Assert.Equal(0, options.MaximumAutoDownloadConnectionsPerIpAddress);
        Assert.Equal(0, options.MaximumAutoDownloadRequestsGlobally);
        Assert.Equal(0, options.MaximumAutoRefreshRequestsPerDestination);
        Assert.Equal(0, options.MaximumAutoRefreshRequestsGlobally);
        Assert.Equal(1, options.MaximumFramesPerError);
        Assert.Equal(1, options.MaximumProcessingTimeMilliseconds);
        Assert.Equal(1, options.AutoDownloadRefreshIntervalMinutes);
        Assert.Equal(1, options.ParsedSourceMapCacheLifetimeMinutes);
        Assert.Equal(1, options.MaximumParsedSourceMapCacheSize);
    }

    [Fact]
    public async Task EventProcessingAsync_WithUploadedSourceMap_UsesOriginalNameForStackSignature()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        var serializer = GetService<ITextSerializer>();
        var options = GetService<AppOptions>();
        var loggerFactory = GetService<ILoggerFactory>();
        var sourceMapPlugin = new SourceMapPlugin(service, serializer, GetService<BillingPlans>(), options, loggerFactory);
        var errorPlugin = new ErrorPlugin(serializer, options, loggerFactory);
        var persistentEvent = new PersistentEvent { Type = Event.KnownTypes.Error };
        persistentEvent.SetError(CreateError());
        var context = new EventContext(
            persistentEvent,
            new Organization { Id = "507f1f77bcf86cd799439012" },
            new Project { Id = ProjectId, OrganizationId = "507f1f77bcf86cd799439012" });

        await sourceMapPlugin.EventProcessingAsync(context);
        await errorPlugin.EventProcessingAsync(context);

        Assert.Equal("meaningfulFunction()", context.StackSignatureData["Method"]);
        Assert.True(sourceMapPlugin.HandleError(new IOException("Unavailable"), context));
    }

    private static Error CreateError(string generatedFileUrl = GeneratedFileUrl)
    {
        return new Error
        {
            Message = "Test error",
            Type = "TypeError",
            StackTrace =
            [
                new StackFrame
                {
                    Name = "a",
                    FileName = generatedFileUrl,
                    LineNumber = 1,
                    Column = 1
                }
            ]
        };
    }

    private SourceMapService CreateService(HttpClient httpClient)
    {
        return new SourceMapService(
            new TestHttpClientFactory(httpClient),
            GetService<IFileStorage>(),
            GetService<ICacheClient>(),
            GetService<ILockProvider>(),
            GetService<SourceMapRequestThrottle>(),
            GetService<JsonSerializerOptions>(),
            GetService<AppOptions>(),
            GetService<TimeProvider>(),
            GetService<ILogger<SourceMapService>>());
    }

    private sealed class TestHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private sealed class AsyncDelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request);
        }
    }
}
