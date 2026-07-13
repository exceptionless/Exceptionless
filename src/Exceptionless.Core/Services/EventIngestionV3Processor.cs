using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using System.Text.Json;

namespace Exceptionless.Core.Services;

public interface IEventIngestionProcessor
{
    Task<EventIngestionV3Response> ProcessAsync(
        IReadOnlyCollection<EventIngestionV3Event> sourceEvents,
        Organization organization,
        Project project,
        CancellationToken cancellationToken);
}

public sealed class EventIngestionV3Processor(
    IStackFingerprintService fingerprintService,
    IStackRouteResolver stackRouteResolver,
    IEventMaterializer eventMaterializer,
    IEventBatchWriter eventBatchWriter,
    IIngestionQuotaService quotaService,
    TimeProvider timeProvider) : IEventIngestionProcessor
{
    public async Task<EventIngestionV3Response> ProcessAsync(
        IReadOnlyCollection<EventIngestionV3Event> sourceEvents,
        Organization organization,
        Project project,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = new EventIngestionV3Response { Received = sourceEvents.Count };
        AppDiagnostics.IngestionV3MicroBatchSize.Record(sourceEvents.Count);
        AppDiagnostics.IngestionV3InFlightEvents.Add(sourceEvents.Count);
        using var activity = AppDiagnostics.StartActivity("Ingestion V3 Microbatch");
        try
        {
            if (sourceEvents.Count == 0)
                return response;

            var candidates = new List<Candidate>(sourceEvents.Count);
            using (AppDiagnostics.StartActivity("Ingestion V3 Fingerprint"))
            using (AppDiagnostics.IngestionV3FingerprintTime.StartTimer())
            {
                foreach (var source in sourceEvents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string? validationError = Validate(source);
                    if (validationError is not null)
                    {
                        response.Invalid++;
                        AddError(response, source.Id ?? String.Empty, "validation_error", validationError);
                        continue;
                    }

                    DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
                    DateTime eventDate = source.Date?.UtcDateTime ?? utcNow;
                    double eventAgeInDays = utcNow.Subtract(eventDate).TotalDays;
                    if (eventAgeInDays > 3 || (organization.RetentionDays > 0 && eventAgeInDays > organization.RetentionDays))
                    {
                        response.Discarded++;
                        continue;
                    }

                    candidates.Add(new Candidate(source, fingerprintService.Create(source, organization, project)));
                }
            }

            if (response.Discarded > 0)
                await quotaService.TrackDiscardedAsync(organization.Id, project.Id, response.Discarded);

            if (candidates.Count == 0)
                return response;

            var routes = await stackRouteResolver.ResolveAsync(project.Id, candidates.Select(c => c.Fingerprint.SignatureHash).ToArray(), cancellationToken);
            var survivors = new List<Candidate>(candidates.Count);
            foreach (var candidate in candidates)
            {
                if (routes.TryGetValue(candidate.Fingerprint.SignatureHash, out var route) && route.IsDiscarded)
                    response.Discarded++;
                else
                    survivors.Add(candidate);
            }

            int stackDiscarded = candidates.Count - survivors.Count;
            if (stackDiscarded > 0)
                await quotaService.TrackDiscardedAsync(organization.Id, project.Id, stackDiscarded);

            if (survivors.Count == 0)
                return response;

            int admittedCount;
            using (AppDiagnostics.StartActivity("Ingestion V3 Quota Reserve"))
            using (AppDiagnostics.IngestionV3QuotaTime.StartTimer())
                admittedCount = await quotaService.ReserveAsync(organization.Id, survivors.Count);
            if (admittedCount < survivors.Count)
            {
                response.Blocked = survivors.Count - admittedCount;
                await quotaService.TrackBlockedAsync(organization.Id, project.Id, response.Blocked);
            }

            if (admittedCount == 0)
                return response;

            try
            {
                var writes = new List<EventIngestionWrite>(admittedCount);
                for (int i = 0; i < admittedCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Candidate candidate = survivors[i];
                    routes.TryGetValue(candidate.Fingerprint.SignatureHash, out var route);
                    PersistentEvent ev;
                    using (AppDiagnostics.IngestionV3MaterializationTime.StartTimer())
                        ev = eventMaterializer.Materialize(candidate.Source, candidate.Fingerprint, organization, project);
                    writes.Add(new EventIngestionWrite(candidate.Source.Id, ev, candidate.Fingerprint, route));
                }

                EventBatchWriteResult result;
                using (AppDiagnostics.IngestionV3WriteTime.StartTimer())
                    result = await eventBatchWriter.WriteAsync(writes, organization, project, cancellationToken);
                response.Persisted = result.Persisted;
                response.Duplicate = result.Duplicate;

                if (result.Persisted > 0)
                {
                    using (AppDiagnostics.StartActivity("Ingestion V3 Quota Settle"))
                    using (AppDiagnostics.IngestionV3SettlementTime.StartTimer())
                        await quotaService.CommitAsync(organization.Id, project.Id, result.Persisted);
                }

                return response;
            }
            finally
            {
                await quotaService.ReleaseAsync(organization.Id, admittedCount);
            }
        }
        finally
        {
            AppDiagnostics.IngestionV3Received.Add(response.Received);
            AppDiagnostics.IngestionV3Persisted.Add(response.Persisted);
            AppDiagnostics.IngestionV3Discarded.Add(response.Discarded);
            AppDiagnostics.IngestionV3Duplicate.Add(response.Duplicate);
            AppDiagnostics.IngestionV3Blocked.Add(response.Blocked);
            AppDiagnostics.IngestionV3Invalid.Add(response.Invalid);
            AppDiagnostics.IngestionV3InFlightEvents.Add(-sourceEvents.Count);
        }
    }

    private static string? Validate(EventIngestionV3Event source)
    {
        if (String.IsNullOrWhiteSpace(source.Id) || source.Id.Length > EventIngestionV3Limits.MaximumEventIdLength)
            return $"id must contain between 1 and {EventIngestionV3Limits.MaximumEventIdLength} characters.";
        if (String.IsNullOrWhiteSpace(source.Type) || source.Type.Length > EventIngestionV3Limits.MaximumTypeLength)
            return $"type must contain between 1 and {EventIngestionV3Limits.MaximumTypeLength} characters.";
        if (source.Source?.Length > EventIngestionV3Limits.MaximumSourceLength)
            return $"source cannot exceed {EventIngestionV3Limits.MaximumSourceLength} characters.";
        if (source.Message?.Length > EventIngestionV3Limits.MaximumMessageLength)
            return $"message cannot exceed {EventIngestionV3Limits.MaximumMessageLength} characters.";
        if (source.ReferenceId?.Length > EventIngestionV3Limits.MaximumReferenceIdLength)
            return $"reference_id cannot exceed {EventIngestionV3Limits.MaximumReferenceIdLength} characters.";
        if (source.ExceptionType?.Length > EventIngestionV3Limits.MaximumExceptionTypeLength)
            return $"exception_type cannot exceed {EventIngestionV3Limits.MaximumExceptionTypeLength} characters.";
        if (source.StackTrace?.Length > EventIngestionV3Limits.MaximumStackTraceLength)
            return $"stack_trace cannot exceed {EventIngestionV3Limits.MaximumStackTraceLength} characters.";
        if (source.Version is not null && (String.IsNullOrWhiteSpace(source.Version) || source.Version.Length > EventIngestionV3Limits.MaximumVersionLength))
            return $"version must contain between 1 and {EventIngestionV3Limits.MaximumVersionLength} characters.";
        if (source.Level is not null && (String.IsNullOrWhiteSpace(source.Level) || source.Level.Length > EventIngestionV3Limits.MaximumLevelLength))
            return $"level must contain between 1 and {EventIngestionV3Limits.MaximumLevelLength} characters.";
        if (source.Client is not null)
        {
            if (String.IsNullOrWhiteSpace(source.Client.Name) || source.Client.Name.Length > EventIngestionV3Limits.MaximumClientNameLength)
                return $"client.name must contain between 1 and {EventIngestionV3Limits.MaximumClientNameLength} characters.";
            if (String.IsNullOrWhiteSpace(source.Client.Version) || source.Client.Version.Length > EventIngestionV3Limits.MaximumClientVersionLength)
                return $"client.version must contain between 1 and {EventIngestionV3Limits.MaximumClientVersionLength} characters.";
        }
        if (source.Stacking?.Title?.Length > EventIngestionV3Limits.MaximumMessageLength)
            return $"stacking.title cannot exceed {EventIngestionV3Limits.MaximumMessageLength} characters.";
        string? stackingError = ValidateDictionary(source.Stacking?.SignatureData, "stacking.signature_data", value => value.Length <= EventIngestionV3Limits.MaximumMetadataValueLength);
        if (stackingError is not null)
            return stackingError;
        if (source.Tags is { Length: > EventIngestionV3Limits.MaximumTags })
            return $"tags cannot contain more than {EventIngestionV3Limits.MaximumTags} values.";
        if (source.Tags?.Any(tag => String.IsNullOrWhiteSpace(tag) || tag.Length > EventIngestionV3Limits.MaximumTagLength) is true)
            return $"tags must be non-empty and cannot exceed {EventIngestionV3Limits.MaximumTagLength} characters.";
        if (source.User?.Identity?.Length > EventIngestionV3Limits.MaximumUserIdentityLength)
            return $"user.identity cannot exceed {EventIngestionV3Limits.MaximumUserIdentityLength} characters.";
        if (source.User?.Name?.Length > EventIngestionV3Limits.MaximumUserNameLength)
            return $"user.name cannot exceed {EventIngestionV3Limits.MaximumUserNameLength} characters.";
        if (source.Request is not null)
        {
            string? requestError = ValidateRequest(source.Request);
            if (requestError is not null)
                return requestError;
        }
        if (source.Environment is not null)
        {
            string? environmentError = ValidateEnvironment(source.Environment);
            if (environmentError is not null)
                return environmentError;
        }

        string? dataError = ValidateJson(source.Data, "data");
        if (dataError is not null)
            return dataError;
        dataError = ValidateJson(source.User?.Data, "user.data");
        if (dataError is not null)
            return dataError;

        return null;
    }

    private static string? ValidateRequest(EventIngestionV3Request request)
    {
        if (request.UserAgent?.Length > EventIngestionV3Limits.MaximumMetadataValueLength)
            return $"request.user_agent cannot exceed {EventIngestionV3Limits.MaximumMetadataValueLength} characters.";
        if (request.HttpMethod?.Length > 32)
            return "request.http_method cannot exceed 32 characters.";
        if (request.Host?.Length > 255)
            return "request.host cannot exceed 255 characters.";
        if (request.Path?.Length > EventIngestionV3Limits.MaximumMetadataValueLength)
            return $"request.path cannot exceed {EventIngestionV3Limits.MaximumMetadataValueLength} characters.";
        if (request.Referrer?.Length > EventIngestionV3Limits.MaximumMetadataValueLength)
            return $"request.referrer cannot exceed {EventIngestionV3Limits.MaximumMetadataValueLength} characters.";
        if (request.ClientIpAddress?.Length > 64)
            return "request.client_ip_address cannot exceed 64 characters.";

        string? dictionaryError = ValidateDictionary(request.Headers, "request.headers", values => values.Length <= EventIngestionV3Limits.MaximumMetadataEntries
            && values.All(value => value.Length <= EventIngestionV3Limits.MaximumMetadataValueLength));
        if (dictionaryError is not null)
            return dictionaryError;
        dictionaryError = ValidateDictionary(request.Cookies, "request.cookies", value => value.Length <= EventIngestionV3Limits.MaximumMetadataValueLength);
        if (dictionaryError is not null)
            return dictionaryError;
        dictionaryError = ValidateDictionary(request.QueryString, "request.query_string", value => value.Length <= EventIngestionV3Limits.MaximumMetadataValueLength);
        if (dictionaryError is not null)
            return dictionaryError;

        string? dataError = ValidateJson(request.PostData, "request.post_data");
        return dataError ?? ValidateJson(request.Data, "request.data");
    }

    private static string? ValidateEnvironment(EventIngestionV3Environment environment)
    {
        string?[] values =
        [
            environment.Architecture,
            environment.OSName,
            environment.OSVersion,
            environment.MachineName,
            environment.RuntimeVersion,
            environment.ProcessName,
            environment.ProcessId,
            environment.ThreadName,
            environment.ThreadId
        ];
        if (values.Any(value => value?.Length > EventIngestionV3Limits.MaximumMetadataValueLength))
            return $"environment string values cannot exceed {EventIngestionV3Limits.MaximumMetadataValueLength} characters.";

        return ValidateJson(environment.Data, "environment.data");
    }

    private static string? ValidateDictionary<TValue>(IReadOnlyDictionary<string, TValue>? dictionary, string path, Func<TValue, bool> validateValue)
    {
        if (dictionary is null)
            return null;
        if (dictionary.Count > EventIngestionV3Limits.MaximumMetadataEntries)
            return $"{path} cannot contain more than {EventIngestionV3Limits.MaximumMetadataEntries} entries.";
        if (dictionary.Any(pair => pair.Key.Length > EventIngestionV3Limits.MaximumMetadataKeyLength || !validateValue(pair.Value)))
            return $"{path} contains a key or value that exceeds its limit.";
        return null;
    }

    private static string? ValidateJson(JsonElement? element, string path)
    {
        if (element is null)
            return null;

        int tokens = 0;
        return ValidateJsonValue(element.Value, path, ref tokens);
    }

    private static string? ValidateJsonValue(JsonElement element, string path, ref int tokens)
    {
        tokens++;
        if (tokens > EventIngestionV3Limits.MaximumDataTokens)
            return $"{path} cannot contain more than {EventIngestionV3Limits.MaximumDataTokens} JSON values.";

        if (element.ValueKind == JsonValueKind.String && element.GetString()?.Length > EventIngestionV3Limits.MaximumDataStringLength)
            return $"{path} contains a string longer than {EventIngestionV3Limits.MaximumDataStringLength} characters.";

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Length > EventIngestionV3Limits.MaximumMetadataKeyLength)
                    return $"{path} contains a property name longer than {EventIngestionV3Limits.MaximumMetadataKeyLength} characters.";
                string? error = ValidateJsonValue(property.Value, path, ref tokens);
                if (error is not null)
                    return error;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                string? error = ValidateJsonValue(item, path, ref tokens);
                if (error is not null)
                    return error;
            }
        }

        return null;
    }

    private static void AddError(EventIngestionV3Response response, string id, string code, string message)
    {
        if (response.Errors.Count < 100)
            response.Errors.Add(new EventIngestionV3Error(id, code, message));
    }

    private sealed record Candidate(EventIngestionV3Event Source, StackFingerprint Fingerprint);
}
