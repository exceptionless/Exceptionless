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
    private static readonly byte[] SourceMapWithoutNames = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/app.ts"],"names":[],"mappings":"AAAA"}""");
    private static readonly byte[] UpdatedSourceMap = Encoding.UTF8.GetBytes("""{"version":3,"sources":["src/updated.ts"],"names":["updatedFunction"],"mappings":"AAAAA"}""");

    public SourceMapServiceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void CreateSourceMapHttpMessageHandler_ForGeneratedFileProbe_DisablesAutomaticDecompression()
    {
        using var handler = Bootstrapper.CreateSourceMapHttpMessageHandler(GetService<SourceMapRequestThrottle>(), DecompressionMethods.None);

        Assert.Equal(DecompressionMethods.None, handler.AutomaticDecompression);
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
    public async Task SymbolicateAsync_WithoutGeneratedColumn_LeavesFrameUnchanged()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        var error = CreateError();
        var frame = Assert.Single(error.StackTrace!);
        frame.Column = null;
        var serializer = GetService<ITextSerializer>();
        string originalFrame = serializer.SerializeToString(frame);

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.False(changed);
        Assert.Equal(originalFrame, serializer.SerializeToString(frame));
    }

    [Fact]
    public async Task SymbolicateAsync_WithZeroGeneratedColumn_LeavesFrameUnchanged()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        await service.SaveUploadedAsync(ProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        var error = CreateError();
        var frame = Assert.Single(error.StackTrace!);
        frame.Column = 0;
        var serializer = GetService<ITextSerializer>();
        string originalFrame = serializer.SerializeToString(frame);

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.False(changed);
        Assert.Equal(originalFrame, serializer.SerializeToString(frame));
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
        var options = GetService<AppOptions>();
        var requestedUris = new List<Uri>();
        var requestedClientNames = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                Assert.Equal("bytes=-65536", request.Headers.Range?.ToString());
                Assert.Equal("identity", Assert.Single(request.Headers.AcceptEncoding).Value);
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Content.Headers.ContentLength = options.SourceMapOptions.MaximumGeneratedFileSize + 1;
                response.Headers.TryAddWithoutValidation("SourceMap", "app.min.js.map");
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        var service = new SourceMapService(
            new TestHttpClientFactory(httpClient, requestedClientNames),
            GetService<IFileStorage>(),
            GetService<ICacheClient>(),
            GetService<ILockProvider>(),
            GetService<SourceMapRequestThrottle>(),
            GetService<JsonSerializerOptions>(),
            options,
            GetService<TimeProvider>(),
            GetService<ILogger<SourceMapService>>());
        var error = CreateError();

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal([new Uri(GeneratedFileUrl), new Uri("https://cdn.example.com/assets/app.min.js.map")], requestedUris);
        Assert.Equal([SourceMapService.GeneratedFileHttpClientName, SourceMapService.HttpClientName], requestedClientNames);
        var artifact = Assert.Single(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.True(artifact.IsAutoDownloaded);
        Assert.Equal("https://cdn.example.com/assets/app.min.js.map", artifact.SourceMapUrl);
    }

    [Fact]
    public async Task SymbolicateAsync_WithOversizedGeneratedFile_UsesConventionalSourceMapUrl()
    {
        var options = GetService<AppOptions>();
        var requestedUris = new List<Uri>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                Assert.Equal("bytes=-65536", request.Headers.Range?.ToString());
                Assert.Equal("identity", Assert.Single(request.Headers.AcceptEncoding).Value);
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
                response.Content.Headers.ContentLength = options.SourceMapOptions.MaximumGeneratedFileSize + 1;
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        var error = CreateError();

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal([new Uri(GeneratedFileUrl), new Uri("https://cdn.example.com/assets/app.min.js.map")], requestedUris);
        var artifact = Assert.Single(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.True(artifact.IsAutoDownloaded);
        Assert.Equal("https://cdn.example.com/assets/app.min.js.map", artifact.SourceMapUrl);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenVersionedConventionalSourceMapUrlIsMissing_RetriesWithoutQuery()
    {
        string generatedFileUrl = GeneratedFileUrl + "?v=123";
        var requestedUris = new List<Uri>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(generatedFileUrl))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
            }

            if (request.RequestUri == new Uri(GeneratedFileUrl + ".map?v=123"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        Assert.True(await service.SymbolicateAsync(ProjectId, CreateError(generatedFileUrl), TestContext.Current.CancellationToken));
        Assert.Equal(
            [new Uri(generatedFileUrl), new Uri(GeneratedFileUrl + ".map?v=123"), new Uri(GeneratedFileUrl + ".map")],
            requestedUris);
        var artifact = Assert.Single(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.Equal(GeneratedFileUrl + ".map", artifact.SourceMapUrl);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenVersionedConventionalSourceMapIsInvalid_RetriesWithoutQuery()
    {
        string generatedFileUrl = GeneratedFileUrl + "?v=123";
        var requestedUris = new List<Uri>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(generatedFileUrl))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
            }

            return request.RequestUri == new Uri(GeneratedFileUrl + ".map?v=123")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not a source map") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        Assert.True(await service.SymbolicateAsync(ProjectId, CreateError(generatedFileUrl), TestContext.Current.CancellationToken));
        Assert.Equal(
            [new Uri(generatedFileUrl), new Uri(GeneratedFileUrl + ".map?v=123"), new Uri(GeneratedFileUrl + ".map")],
            requestedUris);
    }

    [Fact]
    public async Task SymbolicateAsync_WithVersionedConventionalSourceMapUrl_PreservesQuery()
    {
        string generatedFileUrl = GeneratedFileUrl + "?v=123";
        var requestedUris = new List<Uri>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(generatedFileUrl))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("minified") };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        Assert.True(await service.SymbolicateAsync(ProjectId, CreateError(generatedFileUrl), TestContext.Current.CancellationToken));
        Assert.Equal([new Uri(generatedFileUrl), new Uri(GeneratedFileUrl + ".map?v=123")], requestedUris);
        var artifact = Assert.Single(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.Equal(GeneratedFileUrl + ".map?v=123", artifact.SourceMapUrl);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenSuffixRangeIsNotSatisfiable_RetriesWithoutRange()
    {
        int generatedFileRequestCount = 0;
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                generatedFileRequestCount++;
                Assert.Equal("identity", Assert.Single(request.Headers.AcceptEncoding).Value);
                if (generatedFileRequestCount == 1)
                {
                    Assert.Equal("bytes=-65536", request.Headers.Range?.ToString());
                    return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
                }

                Assert.Null(request.Headers.Range);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("minified\n//# sourceMappingURL=app.min.js.map")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        var error = CreateError();

        bool changed = await service.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(2, generatedFileRequestCount);
        Assert.Equal("meaningfulFunction", Assert.Single(error.StackTrace!).Name);
        var artifact = Assert.Single(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.True(artifact.IsAutoDownloaded);
        Assert.Equal("https://cdn.example.com/assets/app.min.js.map", artifact.SourceMapUrl);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenSourceMapMarkerIsInsideString_UsesConventionalSourceMapUrl()
    {
        var requestedUris = new List<Uri>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("const marker = \"//# sourceMappingURL=bogus.map\";")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        Assert.True(await service.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));
        Assert.Equal([new Uri(GeneratedFileUrl), new Uri(GeneratedFileUrl + ".map")], requestedUris);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenRangedContentStartsInsideString_FindsTrailingSourceMapComment()
    {
        var requestedUris = new List<Uri>();
        var handler = new DelegateHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            if (request.RequestUri == new Uri(GeneratedFileUrl))
            {
                return new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new StringContent("truncated string value\";//# sourceMappingURL=custom.map")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(SourceMap) };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        Assert.True(await service.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));
        Assert.Equal([new Uri(GeneratedFileUrl), new Uri("https://cdn.example.com/assets/custom.map")], requestedUris);
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
    public async Task SymbolicateAsync_WhenParsedCacheOutlivesRefreshInterval_RefreshesCachedMap()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.AutoDownloadRefreshIntervalMinutes = 1;
        options.SourceMapOptions.ParsedSourceMapCacheLifetimeMinutes = 120;
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
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(sourceMapDownloadCount == 1 ? SourceMap : UpdatedSourceMap)
            };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        var initialError = CreateError();
        Assert.True(await service.SymbolicateAsync(ProjectId, initialError, TestContext.Current.CancellationToken));
        Assert.Equal("meaningfulFunction", Assert.Single(initialError.StackTrace!).Name);

        TimeProvider.Advance(TimeSpan.FromMinutes(2));

        var refreshedError = CreateError();
        Assert.True(await service.SymbolicateAsync(ProjectId, refreshedError, TestContext.Current.CancellationToken));
        Assert.Equal("updatedFunction", Assert.Single(refreshedError.StackTrace!).Name);
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
    public async Task SymbolicateAsync_WhenDownloadedSourceMapIsInvalid_DoesNotPersistArtifact()
    {
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

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not a source map") };
        });
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        Assert.False(await service.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));
        Assert.Empty(await service.GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.False(await service.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task SymbolicateAsync_WhenDownloadedRefreshIsInvalid_PreservesStoredMap()
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
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = sourceMapDownloadCount == 1 ? new ByteArrayContent(SourceMap) : new StringContent("not a source map")
            };
        });
        using var httpClient = new HttpClient(handler);
        using (var initialService = CreateService(httpClient))
            Assert.True(await initialService.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));
        var initialArtifact = Assert.Single(await GetService<SourceMapService>().GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));

        TimeProvider.Advance(TimeSpan.FromMinutes(61));

        using (var refreshingService = CreateService(httpClient))
            Assert.False(await refreshingService.SymbolicateAsync(ProjectId, CreateError(), TestContext.Current.CancellationToken));
        var preservedArtifact = Assert.Single(await GetService<SourceMapService>().GetArtifactsAsync(ProjectId, TestContext.Current.CancellationToken));
        Assert.Equal(initialArtifact, preservedArtifact);

        GetService<AppOptions>().SourceMapOptions.EnableAutoDownload = false;
        using var storedMapService = CreateService(httpClient);
        var error = CreateError();
        Assert.True(await storedMapService.SymbolicateAsync(ProjectId, error, TestContext.Current.CancellationToken));
        Assert.Equal("meaningfulFunction", Assert.Single(error.StackTrace!).Name);
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
    public async Task SaveUploadedAsync_WhenSupersededMapCannotBeDeleted_RollsBackReplacement()
    {
        var storage = GetService<IFileStorage>();
        var initialService = GetService<SourceMapService>();
        string projectId = $"project-{Guid.NewGuid():N}";
        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await initialService.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        var initialMap = Assert.Single(await storage.GetFileListAsync($"source-maps/{projectId}/*.map", cancellationToken: TestContext.Current.CancellationToken));

        using var httpClient = new HttpClient(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var replacementService = CreateService(httpClient, new DeleteInterceptingFileStorage(storage, initialMap.Path));
        await using var updatedSourceMapStream = new MemoryStream(UpdatedSourceMap);

        await Assert.ThrowsAsync<IOException>(() => replacementService.SaveUploadedAsync(
            projectId,
            GeneratedFileUrl,
            "app.min.js.map",
            updatedSourceMapStream,
            TestContext.Current.CancellationToken));

        var error = CreateError();
        Assert.True(await initialService.SymbolicateAsync(projectId, error, TestContext.Current.CancellationToken));
        Assert.Equal("meaningfulFunction", Assert.Single(error.StackTrace!).Name);
        Assert.Equal(initialMap.Path, Assert.Single(await storage.GetFileListAsync($"source-maps/{projectId}/*.map", cancellationToken: TestContext.Current.CancellationToken)).Path);
    }

    [Fact]
    public async Task SaveUploadedAsync_WhenSupersededMapCleanupThrows_RollsBackReplacement()
    {
        var storage = GetService<IFileStorage>();
        var initialService = GetService<SourceMapService>();
        string projectId = $"project-{Guid.NewGuid():N}";
        await using (var sourceMapStream = new MemoryStream(SourceMap))
        {
            await initialService.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        }
        var initialMap = Assert.Single(await storage.GetFileListAsync($"source-maps/{projectId}/*.map", cancellationToken: TestContext.Current.CancellationToken));

        using var httpClient = new HttpClient(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var replacementService = CreateService(httpClient, new DeleteInterceptingFileStorage(storage, initialMap.Path, throwOnDelete: true));
        await using var updatedSourceMapStream = new MemoryStream(UpdatedSourceMap);

        await Assert.ThrowsAsync<IOException>(() => replacementService.SaveUploadedAsync(
            projectId,
            GeneratedFileUrl,
            "app.min.js.map",
            updatedSourceMapStream,
            TestContext.Current.CancellationToken));

        var error = CreateError();
        Assert.True(await initialService.SymbolicateAsync(projectId, error, TestContext.Current.CancellationToken));
        Assert.Equal("meaningfulFunction", Assert.Single(error.StackTrace!).Name);
        Assert.Equal(initialMap.Path, Assert.Single(await storage.GetFileListAsync($"source-maps/{projectId}/*.map", cancellationToken: TestContext.Current.CancellationToken)).Path);
    }

    [Fact]
    public async Task SaveUploadedAsync_WhenAnotherServiceHasCachedMap_InvalidatesCachedMap()
    {
        using var httpClient = new HttpClient(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var uploadingService = CreateService(httpClient);
        using var processingService = CreateService(httpClient);
        string projectId = $"project-{Guid.NewGuid():N}";

        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await uploadingService.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        var initialError = CreateError();
        Assert.True(await processingService.SymbolicateAsync(projectId, initialError, TestContext.Current.CancellationToken));
        Assert.Equal("meaningfulFunction", Assert.Single(initialError.StackTrace!).Name);

        await using (var sourceMapStream = new MemoryStream(UpdatedSourceMap))
            await uploadingService.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        var updatedError = CreateError();
        Assert.True(await processingService.SymbolicateAsync(projectId, updatedError, TestContext.Current.CancellationToken));
        Assert.Equal("updatedFunction", Assert.Single(updatedError.StackTrace!).Name);
    }

    [Fact]
    public async Task SaveUploadedAsync_WithLegacyExpiringCacheGeneration_PersistsAdvancedGeneration()
    {
        string projectId = $"project-{Guid.NewGuid():N}";
        string cacheKey = $"source-maps:cache-version:{projectId}";
        var cache = GetService<ICacheClient>();
        Assert.True(await cache.SetAsync(cacheKey, 7L, TimeSpan.FromDays(1)));
        var service = GetService<SourceMapService>();

        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        TimeProvider.Advance(TimeSpan.FromDays(2));
        Assert.Equal(8, (await cache.GetAsync<long>(cacheKey)).Value);
    }

    [Fact]
    public async Task DeleteArtifactAsync_WhenAnotherServiceHasCachedMap_InvalidatesCachedMap()
    {
        using var httpClient = new HttpClient(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var deletingService = CreateService(httpClient);
        using var processingService = CreateService(httpClient);
        string projectId = $"project-{Guid.NewGuid():N}";

        SourceMapArtifact artifact;
        await using (var sourceMapStream = new MemoryStream(SourceMap))
            artifact = await deletingService.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        Assert.True(await processingService.SymbolicateAsync(projectId, CreateError(), TestContext.Current.CancellationToken));
        Assert.True(await deletingService.DeleteArtifactAsync(projectId, artifact.Id, TestContext.Current.CancellationToken));

        var error = CreateError();
        Assert.False(await processingService.SymbolicateAsync(projectId, error, TestContext.Current.CancellationToken));
        Assert.Equal("a", Assert.Single(error.StackTrace!).Name);
    }

    [Fact]
    public async Task DeleteArtifactAsync_WhenMetadataCannotBeDeleted_ReportsFailureAndPreservesArtifact()
    {
        var storage = GetService<IFileStorage>();
        var initialService = GetService<SourceMapService>();
        string projectId = $"project-{Guid.NewGuid():N}";
        SourceMapArtifact artifact;
        await using (var sourceMapStream = new MemoryStream(SourceMap))
            artifact = await initialService.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        string metadataPath = $"source-maps/{projectId}/{artifact.Id}.json";
        using var httpClient = new HttpClient(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var deletingService = CreateService(httpClient, new DeleteInterceptingFileStorage(storage, metadataPath));

        Assert.False(await deletingService.DeleteArtifactAsync(projectId, artifact.Id, TestContext.Current.CancellationToken));
        Assert.True(await storage.ExistsAsync(metadataPath));
        Assert.Single(await initialService.GetArtifactsAsync(projectId, TestContext.Current.CancellationToken));
        Assert.Single(await storage.GetFileListAsync($"source-maps/{projectId}/*.map", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveUploadedAsync_WhenArtifactLimitIsReached_RejectsNewUrlButAllowsReplacement()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumArtifactsPerProject = 1;
        options.SourceMapOptions.MaximumStorageSizePerProject = 1024 * 1024;
        var service = GetService<SourceMapService>();
        string projectId = $"project-{Guid.NewGuid():N}";

        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        await using (var sourceMapStream = new MemoryStream(UpdatedSourceMap))
            await service.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        await using var secondSourceMapStream = new MemoryStream(SourceMap);

        var exception = await Assert.ThrowsAsync<SourceMapStorageLimitException>(() => service.SaveUploadedAsync(
            projectId,
            "https://cdn.example.com/assets/second.min.js",
            "second.min.js.map",
            secondSourceMapStream,
            TestContext.Current.CancellationToken));

        Assert.Contains("artifact limit of 1", exception.Message);
        Assert.Single(await service.GetArtifactsAsync(projectId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveUploadedAsync_WhenStorageSizeLimitIsReached_RejectsNewMap()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumArtifactsPerProject = 10;
        options.SourceMapOptions.MaximumStorageSizePerProject = SourceMap.LongLength;
        var service = GetService<SourceMapService>();
        string projectId = $"project-{Guid.NewGuid():N}";

        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);
        await using var secondSourceMapStream = new MemoryStream(SourceMap);

        await Assert.ThrowsAsync<SourceMapStorageLimitException>(() => service.SaveUploadedAsync(
            projectId,
            "https://cdn.example.com/assets/second.min.js",
            "second.min.js.map",
            secondSourceMapStream,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveUploadedAsync_ForFreePlan_UsesSmallerArtifactLimit()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumArtifactsPerFreeProject = 1;
        options.SourceMapOptions.MaximumArtifactsPerProject = 2;
        options.SourceMapOptions.MaximumStorageSizePerFreeProject = 1024 * 1024;
        options.SourceMapOptions.MaximumStorageSizePerProject = 1024 * 1024;
        var service = GetService<SourceMapService>();
        string freeProjectId = $"free-{Guid.NewGuid():N}";
        string paidProjectId = $"paid-{Guid.NewGuid():N}";

        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(freeProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, true, TestContext.Current.CancellationToken);
        await using (var sourceMapStream = new MemoryStream(SourceMap))
        {
            var exception = await Assert.ThrowsAsync<SourceMapStorageLimitException>(() => service.SaveUploadedAsync(
                freeProjectId,
                "https://cdn.example.com/assets/second.min.js",
                "second.min.js.map",
                sourceMapStream,
                true,
                TestContext.Current.CancellationToken));
            Assert.Contains("artifact limit of 1", exception.Message);
        }

        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(paidProjectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, false, TestContext.Current.CancellationToken);
        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(paidProjectId, "https://cdn.example.com/assets/second.min.js", "second.min.js.map", sourceMapStream, false, TestContext.Current.CancellationToken);

        Assert.Single(await service.GetArtifactsAsync(freeProjectId, TestContext.Current.CancellationToken));
        Assert.Equal(2, (await service.GetArtifactsAsync(paidProjectId, TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task CleanupStaleArtifactsAsync_WhenArtifactWasUsed_RetainsUntilUsageBecomesStale()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.FreeArtifactRetentionDays = 14;
        var service = GetService<SourceMapService>();
        string projectId = $"project-{Guid.NewGuid():N}";
        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, true, TestContext.Current.CancellationToken);

        TimeProvider.Advance(TimeSpan.FromDays(15));
        Assert.True(await service.SymbolicateAsync(projectId, CreateError(), TestContext.Current.CancellationToken));

        Assert.Equal(0, await service.CleanupStaleArtifactsAsync(projectId, true, TestContext.Current.CancellationToken));
        var retained = Assert.Single(await service.GetArtifactsAsync(projectId, TestContext.Current.CancellationToken));
        Assert.Equal(TimeProvider.GetUtcNow().UtcDateTime, retained.LastUsedUtc!.Value, TimeSpan.FromMilliseconds(1));

        await service.SaveUsagesAsync(TestContext.Current.CancellationToken);
        TimeProvider.Advance(TimeSpan.FromDays(15));

        Assert.Equal(1, await service.CleanupStaleArtifactsAsync(projectId, true, TestContext.Current.CancellationToken));
        Assert.Empty(await service.GetArtifactsAsync(projectId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CleanupStaleArtifactsAsync_WhenArtifactWasNeverUsed_RemovesItAfterRetention()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.FreeArtifactRetentionDays = 14;
        var service = GetService<SourceMapService>();
        string projectId = $"project-{Guid.NewGuid():N}";
        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, true, TestContext.Current.CancellationToken);

        TimeProvider.Advance(TimeSpan.FromDays(15));

        Assert.Equal(1, await service.CleanupStaleArtifactsAsync(projectId, true, TestContext.Current.CancellationToken));
        Assert.Empty(await service.GetArtifactsAsync(projectId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CleanupStaleArtifactsAsync_WhenSymbolicationRacesDeletion_DoesNotDeleteSuccessfullyUsedMap()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.FreeArtifactRetentionDays = 14;
        var storage = new BlockingDeleteFileStorage(GetService<IFileStorage>());
        using var httpClient = new HttpClient(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var service = CreateService(httpClient, storage);
        string projectId = $"project-{Guid.NewGuid():N}";
        await using (var sourceMapStream = new MemoryStream(SourceMap))
            await service.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, true, TestContext.Current.CancellationToken);

        TimeProvider.Advance(TimeSpan.FromDays(15));
        Task<int> cleanup = service.CleanupStaleArtifactsAsync(projectId, true, TestContext.Current.CancellationToken);
        await storage.DeleteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task<bool> symbolication = service.SymbolicateAsync(projectId, CreateError(), TestContext.Current.CancellationToken);
        Task completedTask = await Task.WhenAny(symbolication, Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        storage.ReleaseDelete.TrySetResult();

        Assert.NotSame(symbolication, completedTask);
        Assert.Equal(1, await cleanup);
        Assert.False(await symbolication);
        Assert.Empty(await service.GetArtifactsAsync(projectId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteProjectArtifactsAsync_AutomaticDownloadInFlight_DoesNotWriteAfterDeletion()
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
        string projectId = $"project-{Guid.NewGuid():N}";

        Task<bool> automaticDownload = service.SymbolicateAsync(projectId, CreateError(), TestContext.Current.CancellationToken);
        await mapRequestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await service.DeleteProjectArtifactsAsync(projectId, TestContext.Current.CancellationToken);
        releaseMapRequest.TrySetResult();

        Assert.False(await automaticDownload);
        Assert.Empty(await service.GetArtifactsAsync(projectId, TestContext.Current.CancellationToken));
        await using var sourceMapStream = new MemoryStream(SourceMap);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveUploadedAsync(
            projectId,
            GeneratedFileUrl,
            "app.min.js.map",
            sourceMapStream,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveUploadedAsync_WhenOrphanedMapExists_RemovesItBeforeCheckingStorageLimit()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumArtifactsPerProject = 10;
        options.SourceMapOptions.MaximumStorageSizePerProject = SourceMap.LongLength;
        var storage = GetService<IFileStorage>();
        string projectId = $"project-{Guid.NewGuid():N}";
        await using (var orphanedMapStream = new MemoryStream(SourceMap, writable: false))
            Assert.True(await storage.SaveFileAsync($"source-maps/{projectId}/orphaned.map", orphanedMapStream, TestContext.Current.CancellationToken));

        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMap);
        await service.SaveUploadedAsync(projectId, GeneratedFileUrl, "app.min.js.map", sourceMapStream, TestContext.Current.CancellationToken);

        var mapFiles = await storage.GetFileListAsync($"source-maps/{projectId}/*.map", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(mapFiles);
        Assert.DoesNotContain(mapFiles, file => file.Path.EndsWith("orphaned.map", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveUploadedAsync_WhenOrphanedMapCannotBeDeleted_CountsItTowardStorageLimit()
    {
        var options = GetService<AppOptions>();
        options.SourceMapOptions.MaximumArtifactsPerProject = 10;
        options.SourceMapOptions.MaximumStorageSizePerProject = SourceMap.LongLength;
        var storage = GetService<IFileStorage>();
        string projectId = $"project-{Guid.NewGuid():N}";
        string orphanedMapPath = $"source-maps/{projectId}/orphaned.map";
        await using (var orphanedMapStream = new MemoryStream(SourceMap, writable: false))
            Assert.True(await storage.SaveFileAsync(orphanedMapPath, orphanedMapStream, TestContext.Current.CancellationToken));

        using var httpClient = new HttpClient(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var service = CreateService(httpClient, new DeleteInterceptingFileStorage(storage, orphanedMapPath));
        await using var sourceMapStream = new MemoryStream(SourceMap);

        await Assert.ThrowsAsync<SourceMapStorageLimitException>(() => service.SaveUploadedAsync(
            projectId,
            GeneratedFileUrl,
            "app.min.js.map",
            sourceMapStream,
            TestContext.Current.CancellationToken));
        Assert.True(await storage.ExistsAsync(orphanedMapPath));
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
            ["SourceMaps:MaximumArtifactsPerProject"] = "0",
            ["SourceMaps:MaximumStorageSizePerProject"] = "0",
            ["SourceMaps:MaximumArtifactsPerFreeProject"] = "0",
            ["SourceMaps:MaximumStorageSizePerFreeProject"] = "0",
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
            ["SourceMaps:MaximumParsedSourceMapCacheSize"] = "0",
            ["SourceMaps:UsageTrackingDebounceMinutes"] = "0",
            ["SourceMaps:FreeArtifactRetentionDays"] = "0",
            ["SourceMaps:ArtifactRetentionDays"] = "0"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var options = SourceMapOptions.ReadFromConfiguration(configuration);

        Assert.Equal(1, options.RequestTimeoutMilliseconds);
        Assert.Equal(1, options.MaximumGeneratedFileSize);
        Assert.Equal(1, options.MaximumSourceMapSize);
        Assert.Equal(1, options.MaximumArtifactsPerProject);
        Assert.Equal(1, options.MaximumStorageSizePerProject);
        Assert.Equal(1, options.MaximumArtifactsPerFreeProject);
        Assert.Equal(1, options.MaximumStorageSizePerFreeProject);
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
        Assert.Equal(1, options.UsageTrackingDebounceMinutes);
        Assert.Equal(1, options.FreeArtifactRetentionDays);
        Assert.Equal(1, options.ArtifactRetentionDays);
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

    [Fact]
    public async Task EventProcessingAsync_WithNamelessSourceMap_UsesOriginalLocationForStackSignature()
    {
        var service = GetService<SourceMapService>();
        await using var sourceMapStream = new MemoryStream(SourceMapWithoutNames);
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

        Assert.Equal("src/app.ts:1:1", context.StackSignatureData["Method"]);
        var processedError = Assert.IsType<Error>(context.Event.Data![Event.KnownDataKeys.Error]);
        var frame = Assert.Single(processedError.StackTrace!);
        Assert.Null(frame.Name);
        var sourceMapData = Assert.IsType<DataDictionary>(frame.Data![StackFrame.KnownDataKeys.SourceMap]);
        Assert.Equal("a", sourceMapData["generated_name"]);
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

    private SourceMapService CreateService(HttpClient httpClient, IFileStorage? storage = null)
    {
        return new SourceMapService(
            new TestHttpClientFactory(httpClient),
            storage ?? GetService<IFileStorage>(),
            GetService<ICacheClient>(),
            GetService<ILockProvider>(),
            GetService<SourceMapRequestThrottle>(),
            GetService<JsonSerializerOptions>(),
            GetService<AppOptions>(),
            GetService<TimeProvider>(),
            GetService<ILogger<SourceMapService>>());
    }

    private sealed class DeleteInterceptingFileStorage(IFileStorage inner, string failingPath, bool throwOnDelete = false) : IFileStorage
    {
        public ISerializer Serializer => inner.Serializer;

        public Task<Stream?> GetFileStreamAsync(string path, StreamMode streamMode, CancellationToken cancellationToken = default)
            => inner.GetFileStreamAsync(path, streamMode, cancellationToken);

        public Task<FileSpec?> GetFileInfoAsync(string path) => inner.GetFileInfoAsync(path);

        public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);

        public Task<bool> SaveFileAsync(string path, Stream stream, CancellationToken cancellationToken = default)
            => inner.SaveFileAsync(path, stream, cancellationToken);

        public Task<bool> RenameFileAsync(string path, string newPath, CancellationToken cancellationToken = default)
            => inner.RenameFileAsync(path, newPath, cancellationToken);

        public Task<bool> CopyFileAsync(string path, string targetPath, CancellationToken cancellationToken = default)
            => inner.CopyFileAsync(path, targetPath, cancellationToken);

        public Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!String.Equals(path, failingPath, StringComparison.Ordinal))
            {
                return inner.DeleteFileAsync(path, cancellationToken);
            }

            if (throwOnDelete)
            {
                throw new IOException("Unable to delete the intercepted file.");
            }

            return Task.FromResult(false);
        }

        public Task<int> DeleteFilesAsync(string? searchPattern = null, CancellationToken cancellation = default)
            => inner.DeleteFilesAsync(searchPattern, cancellation);

        public Task<PagedFileListResult> GetPagedFileListAsync(int pageSize = 100, string? searchPattern = null, CancellationToken cancellationToken = default)
            => inner.GetPagedFileListAsync(pageSize, searchPattern, cancellationToken);

        public void Dispose()
        {
        }
    }

    private sealed class BlockingDeleteFileStorage(IFileStorage inner) : IFileStorage
    {
        public TaskCompletionSource DeleteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDelete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ISerializer Serializer => inner.Serializer;

        public Task<Stream?> GetFileStreamAsync(string path, StreamMode streamMode, CancellationToken cancellationToken = default)
            => inner.GetFileStreamAsync(path, streamMode, cancellationToken);

        public Task<FileSpec?> GetFileInfoAsync(string path) => inner.GetFileInfoAsync(path);

        public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);

        public Task<bool> SaveFileAsync(string path, Stream stream, CancellationToken cancellationToken = default)
            => inner.SaveFileAsync(path, stream, cancellationToken);

        public Task<bool> RenameFileAsync(string path, string newPath, CancellationToken cancellationToken = default)
            => inner.RenameFileAsync(path, newPath, cancellationToken);

        public Task<bool> CopyFileAsync(string path, string targetPath, CancellationToken cancellationToken = default)
            => inner.CopyFileAsync(path, targetPath, cancellationToken);

        public async Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        {
            if (path.EndsWith(".json", StringComparison.Ordinal))
            {
                DeleteStarted.TrySetResult();
                await ReleaseDelete.Task.WaitAsync(cancellationToken);
            }

            return await inner.DeleteFileAsync(path, cancellationToken);
        }

        public Task<int> DeleteFilesAsync(string? searchPattern = null, CancellationToken cancellation = default)
            => inner.DeleteFilesAsync(searchPattern, cancellation);

        public Task<PagedFileListResult> GetPagedFileListAsync(int pageSize = 100, string? searchPattern = null, CancellationToken cancellationToken = default)
            => inner.GetPagedFileListAsync(pageSize, searchPattern, cancellationToken);

        public void Dispose()
        {
        }
    }

    private sealed class TestHttpClientFactory(HttpClient httpClient, ICollection<string>? requestedClientNames = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            requestedClientNames?.Add(name);
            return httpClient;
        }
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
