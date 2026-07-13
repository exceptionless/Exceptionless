using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
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
    SemanticVersionParser semanticVersionParser,
    TimeProvider timeProvider) : IEventIngestionProcessor
{
    private static readonly HashSet<string> _legacyPipelineOnlyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Event.KnownTypes.NotFound,
        Event.KnownTypes.Session,
        Event.KnownTypes.SessionEnd,
        Event.KnownTypes.SessionHeartbeat
    };
    private static readonly HashSet<string> _reservedLegacyDataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        Event.KnownDataKeys.SessionEnd,
        Event.KnownDataKeys.SessionHasError
    };

    public Task<EventIngestionV3Response> ProcessAsync(
        IReadOnlyCollection<EventIngestionV3Event> sourceEvents,
        Organization organization,
        Project project,
        CancellationToken cancellationToken)
    {
        var sources = new SourceEvent[sourceEvents.Count];
        int index = 0;
        foreach (EventIngestionV3Event sourceEvent in sourceEvents)
            sources[index++] = new SourceEvent(sourceEvent);

        return ProcessCoreAsync(sources, organization, project, cancellationToken);
    }

    internal Task<EventIngestionV3Response> ProcessBufferedAsync(
        IReadOnlyCollection<EventIngestionV3BufferedRecord> sourceRecords,
        Organization organization,
        Project project,
        CancellationToken cancellationToken)
    {
        var sources = new SourceEvent[sourceRecords.Count];
        int index = 0;
        foreach (EventIngestionV3BufferedRecord sourceRecord in sourceRecords)
            sources[index++] = new SourceEvent(sourceRecord);

        return ProcessCoreAsync(sources, organization, project, cancellationToken);
    }

    private async Task<EventIngestionV3Response> ProcessCoreAsync(
        IReadOnlyCollection<SourceEvent> sourceEvents,
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

            DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
            var candidates = new List<Candidate>(sourceEvents.Count);
            using (AppDiagnostics.StartActivity("Ingestion V3 Fingerprint"))
            using (AppDiagnostics.IngestionV3FingerprintTime.StartTimer())
            {
                foreach (SourceEvent input in sourceEvents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EventIngestionV3Event source = input.RoutingEvent;
                    string? validationError = ValidateRoutingFields(source);
                    if (validationError is not null)
                    {
                        response.Invalid++;
                        AddError(response, source.Id ?? String.Empty, "validation_error", validationError);
                        continue;
                    }

                    candidates.Add(new Candidate(input, fingerprintService.Create(source, organization, project)));
                }
            }

            if (candidates.Count == 0)
                return response;

            var routes = await stackRouteResolver.ResolveAsync(project.Id, candidates.Select(c => c.Fingerprint.SignatureHash).ToArray(), cancellationToken);
            var survivors = new List<Candidate>(candidates.Count);
            foreach (var routeGroup in candidates.GroupBy(candidate => candidate.Fingerprint.SignatureHash))
            {
                if (!routes.TryGetValue(routeGroup.Key, out var route))
                {
                    survivors.AddRange(routeGroup);
                    continue;
                }

                if (route.IsDiscarded)
                {
                    response.Discarded += routeGroup.Count();
                    continue;
                }

                foreach (var candidate in routeGroup)
                {
                    candidate.Route = route;
                    survivors.Add(candidate);
                }
            }

            int stackDiscarded = candidates.Count - survivors.Count;
            if (stackDiscarded > 0)
                await quotaService.TrackDiscardedAsync(organization.Id, project.Id, stackDiscarded);

            if (survivors.Count == 0)
                return response;

            // Discard routing needs only the small grouping envelope. Validate optional context
            // after known-discarded stacks have terminated so free events do not pay to traverse
            // large metadata bags that will never be materialized or stored.
            for (int index = survivors.Count - 1; index >= 0; index--)
            {
                Candidate candidate = survivors[index];
                candidate.Materialize();
                string? validationError = Validate(candidate.Source);
                if (validationError is null)
                    continue;

                response.Invalid++;
                AddError(response, candidate.Source.Id, "validation_error", validationError);
                survivors.RemoveAt(index);
            }

            if (survivors.Count == 0)
                return response;

            IReadOnlyList<EventIngestionIdentity> identities = await eventBatchWriter.PrepareAsync(
                survivors.Select(candidate => candidate.Source).ToArray(),
                project.Id,
                utcNow,
                cancellationToken);
            var uniqueSurvivors = new List<Candidate>(survivors.Count);
            var duplicateReconciliations = new List<EventIngestionReconciliation>();
            for (int i = 0; i < survivors.Count; i++)
            {
                Candidate candidate = survivors[i];
                EventIngestionIdentity identity = identities[i];
                candidate.Identity = identity;
                if (!identity.IsDuplicate)
                {
                    uniqueSurvivors.Add(candidate);
                    continue;
                }

                response.Duplicate++;
                if (identity.IsPersisted)
                {
                    // A route discarded before this lookup stays on the cheapest path and a
                    // persisted event whose original stack is now discarded has no recovery
                    // side effects or usage settlement. Never reconcile using replay payload data.
                    if (identity.PersistedStackId is null
                        || identity.PersistedStackStatus is null or StackStatus.Discarded
                        || !identity.IsRecoveryEligible)
                        continue;

                    duplicateReconciliations.Add(new EventIngestionReconciliation(
                        identity.EventId,
                        identity.PersistedStackId));
                }
            }

            int uniqueDiscarded = DiscardUniqueCandidates(
                uniqueSurvivors,
                organization,
                utcNow,
                semanticVersionParser);
            if (uniqueDiscarded > 0)
            {
                response.Discarded += uniqueDiscarded;
                await quotaService.TrackDiscardedAsync(organization.Id, project.Id, uniqueDiscarded);
            }

            if (duplicateReconciliations.Count > 0)
                await eventBatchWriter.ReconcileAsync(duplicateReconciliations, organization, project, cancellationToken);

            if (uniqueSurvivors.Count == 0)
                return response;

            EventIngestionReservation reservation;
            using (AppDiagnostics.StartActivity("Ingestion V3 Quota Reserve"))
            using (AppDiagnostics.IngestionV3QuotaTime.StartTimer())
                reservation = await quotaService.ReserveAsync(organization.Id, uniqueSurvivors.Count);
            int admittedCount = reservation.Count;
            if (admittedCount < uniqueSurvivors.Count)
            {
                response.Blocked = uniqueSurvivors.Count - admittedCount;
                await quotaService.TrackBlockedAsync(organization.Id, project.Id, response.Blocked);
            }

            if (admittedCount == 0)
                return response;

            MarkRegressionCandidates(uniqueSurvivors.Take(admittedCount), utcNow, semanticVersionParser);

            bool retainReservationOnFailure = false;
            try
            {
                var writes = new List<EventIngestionWrite>(admittedCount);
                for (int i = 0; i < admittedCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Candidate candidate = uniqueSurvivors[i];
                    PersistentEvent ev;
                    using (AppDiagnostics.IngestionV3MaterializationTime.StartTimer())
                        ev = eventMaterializer.Materialize(candidate.Source, candidate.Fingerprint, organization, project);
                    ev.Id = candidate.Identity!.EventId;
                    ev.CreatedUtc = candidate.Identity.CreatedUtc;
                    ev.Date = candidate.Identity.EventDate;
                    if (candidate.Route?.OccurrencesAreCritical is true)
                        ev.MarkAsCritical();
                    writes.Add(new EventIngestionWrite(
                        candidate.Source.Id,
                        ev,
                        candidate.Fingerprint,
                        candidate.Route,
                        candidate.IsRegressionCandidate));
                }

                EventBatchWriteResult result;
                using (AppDiagnostics.IngestionV3WriteTime.StartTimer())
                    result = await eventBatchWriter.WriteAsync(writes, organization, project, cancellationToken);
                response.Persisted = result.Persisted;
                response.Duplicate += result.Duplicate;

                if (result.Settlements.Count > 0)
                {
                    retainReservationOnFailure = true;
                    using (AppDiagnostics.StartActivity("Ingestion V3 Quota Settle"))
                    using (AppDiagnostics.IngestionV3SettlementTime.StartTimer())
                        await quotaService.CommitAsync(organization.Id, project.Id, result.Settlements);
                    AppDiagnostics.IngestionV3UsageCommitted.Add(result.Settlements.Count);
                    retainReservationOnFailure = false;
                }

                return response;
            }
            catch (EventBatchWriteException ex) when (ex.Settlements.Count > 0)
            {
                retainReservationOnFailure = true;
                using (AppDiagnostics.StartActivity("Ingestion V3 Partial Write Quota Reconcile"))
                using (AppDiagnostics.IngestionV3SettlementTime.StartTimer())
                    await quotaService.CommitAsync(organization.Id, project.Id, ex.Settlements);
                AppDiagnostics.IngestionV3UsageCommitted.Add(ex.Settlements.Count);
                retainReservationOnFailure = false;
                throw;
            }
            finally
            {
                if (!retainReservationOnFailure)
                    await quotaService.ReleaseAsync(reservation);
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
        string? routingError = ValidateRoutingFields(source);
        if (routingError is not null)
            return routingError;

        if (source.Date?.UtcDateTime < DateTime.UnixEpoch)
            return "date cannot be earlier than 1970-01-01T00:00:00Z.";
        if (source.Message?.Length > EventIngestionV3Limits.MaximumMessageLength)
            return $"message cannot exceed {EventIngestionV3Limits.MaximumMessageLength} characters.";
        if (source.ReferenceId is not null
            && (source.ReferenceId.Length is < EventIngestionV3Limits.MinimumReferenceIdLength or > EventIngestionV3Limits.MaximumReferenceIdLength
                || !source.ReferenceId.IsValidIdentifier()))
            return $"reference_id must contain between {EventIngestionV3Limits.MinimumReferenceIdLength} and {EventIngestionV3Limits.MaximumReferenceIdLength} alphanumeric or '-' characters.";
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
        if (source.Stacking?.Title?.Length > EventIngestionV3Limits.MaximumStackTitleLength)
            return $"stacking.title cannot exceed {EventIngestionV3Limits.MaximumStackTitleLength} characters.";
        if (source.Tags is { Length: > EventIngestionV3Limits.MaximumTags })
            return $"tags cannot contain more than {EventIngestionV3Limits.MaximumTags} values.";
        if (source.Tags?.Any(tag => tag is null || String.IsNullOrWhiteSpace(tag) || tag.Length > EventIngestionV3Limits.MaximumTagLength) is true)
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

        string? dataError = ValidateEventData(source.Data);
        if (dataError is not null)
            return dataError;
        dataError = ValidateJsonObject(source.User?.Data, "user.data");
        if (dataError is not null)
            return dataError;

        return null;
    }

    private static string? ValidateRoutingFields(EventIngestionV3Event source)
    {
        if (String.IsNullOrWhiteSpace(source.Id) || source.Id.Length > EventIngestionV3Limits.MaximumEventIdLength)
            return $"id must contain between 1 and {EventIngestionV3Limits.MaximumEventIdLength} characters.";
        if (String.IsNullOrWhiteSpace(source.Type) || source.Type.Length > EventIngestionV3Limits.MaximumTypeLength)
            return $"type must contain between 1 and {EventIngestionV3Limits.MaximumTypeLength} characters.";
        if (_legacyPipelineOnlyTypes.Contains(source.Type))
            return $"type '{source.Type}' is not supported by V3 ingestion; use the V2 endpoint for this legacy stateful event type.";
        if (source.Source?.Length > EventIngestionV3Limits.MaximumSourceLength)
            return $"source cannot exceed {EventIngestionV3Limits.MaximumSourceLength} characters.";
        if (source.ExceptionType?.Length > EventIngestionV3Limits.MaximumExceptionTypeLength)
            return $"exception_type cannot exceed {EventIngestionV3Limits.MaximumExceptionTypeLength} characters.";
        if (source.StackTrace?.Length > EventIngestionV3Limits.MaximumStackTraceLength)
            return $"stack_trace cannot exceed {EventIngestionV3Limits.MaximumStackTraceLength} characters.";
        if (source.Stacking is null)
            return null;
        if (source.Stacking.SignatureData is not { Count: > 0 })
            return "stacking.signature_data must contain at least one value.";
        if (source.Stacking.SignatureData.Any(pair => pair.Value is null))
            return "stacking.signature_data values cannot be null.";

        return ValidateDictionary(
            source.Stacking.SignatureData,
            "stacking.signature_data",
            value => value.Length <= EventIngestionV3Limits.MaximumMetadataValueLength);
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

        string? dictionaryError = ValidateDictionary(request.Headers, "request.headers", values => values is not null
            && values.Length <= EventIngestionV3Limits.MaximumMetadataEntries
            && values.All(value => value is not null && value.Length <= EventIngestionV3Limits.MaximumMetadataValueLength));
        if (dictionaryError is not null)
            return dictionaryError;
        dictionaryError = ValidateDictionary(request.Cookies, "request.cookies", value => value.Length <= EventIngestionV3Limits.MaximumMetadataValueLength);
        if (dictionaryError is not null)
            return dictionaryError;
        dictionaryError = ValidateDictionary(request.QueryString, "request.query_string", value => value.Length <= EventIngestionV3Limits.MaximumMetadataValueLength);
        if (dictionaryError is not null)
            return dictionaryError;

        string? dataError = ValidateJson(request.PostData, "request.post_data");
        return dataError ?? ValidateJsonObject(request.Data, "request.data");
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

        return ValidateJsonObject(environment.Data, "environment.data");
    }

    private static string? ValidateDictionary<TValue>(IReadOnlyDictionary<string, TValue>? dictionary, string path, Func<TValue, bool> validateValue)
    {
        if (dictionary is null)
            return null;
        if (dictionary.Count > EventIngestionV3Limits.MaximumMetadataEntries)
            return $"{path} cannot contain more than {EventIngestionV3Limits.MaximumMetadataEntries} entries.";
        if (dictionary.Any(pair => String.IsNullOrEmpty(pair.Key)
            || pair.Key.Length > EventIngestionV3Limits.MaximumMetadataKeyLength
            || pair.Value is null
            || !validateValue(pair.Value)))
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

    private static string? ValidateJsonObject(JsonElement? element, string path)
    {
        if (element is { ValueKind: not JsonValueKind.Object })
            return $"{path} must be a JSON object.";

        return ValidateJson(element, path);
    }

    private static string? ValidateEventData(JsonElement? element)
    {
        if (element is { ValueKind: not JsonValueKind.Object })
            return "data must be a JSON object.";
        if (element is null)
            return null;

        foreach (JsonProperty property in element.Value.EnumerateObject())
        {
            if (property.Name.StartsWith('@') || _reservedLegacyDataKeys.Contains(property.Name))
                return $"data contains reserved top-level key '{property.Name}'. Use the corresponding first-class V3 field instead.";
        }

        return ValidateJson(element, "data");
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

    private static void MarkRegressionCandidates(
        IEnumerable<Candidate> admittedCandidates,
        DateTime utcNow,
        SemanticVersionParser semanticVersionParser)
    {
        foreach (var routeGroup in admittedCandidates
            .Where(candidate => candidate.Route is { Status: StackStatus.Fixed, DateFixed: not null })
            .GroupBy(candidate => candidate.Fingerprint.SignatureHash))
        {
            StackRoute route = routeGroup.First().Route!;
            Candidate[] orderedCandidates = routeGroup
                .OrderBy(candidate => candidate.Source.Date?.UtcDateTime ?? utcNow)
                .ToArray();
            Candidate? regression = null;
            if (String.IsNullOrEmpty(route.FixedInVersion))
            {
                regression = orderedCandidates.FirstOrDefault(candidate =>
                    (candidate.Source.Date?.UtcDateTime ?? utcNow) > route.DateFixed!.Value);
            }
            else
            {
                var fixedVersion = semanticVersionParser.Parse(route.FixedInVersion);
                if (fixedVersion is not null)
                {
                    foreach (var versionGroup in orderedCandidates.GroupBy(candidate => candidate.Source.Version))
                    {
                        var version = semanticVersionParser.Parse(versionGroup.Key) ?? semanticVersionParser.Default;
                        if (version < fixedVersion)
                            continue;

                        regression = versionGroup.First();
                        break;
                    }
                }
            }

            if (regression is not null)
                regression.IsRegressionCandidate = true;
        }
    }

    private static int DiscardUniqueCandidates(
        List<Candidate> uniqueCandidates,
        Organization organization,
        DateTime utcNow,
        SemanticVersionParser semanticVersionParser)
    {
        int discarded = uniqueCandidates.RemoveAll(candidate =>
        {
            DateTime eventDate = candidate.Source.Date?.UtcDateTime ?? utcNow;
            double eventAgeInDays = utcNow.Subtract(eventDate).TotalDays;
            return eventAgeInDays > 3
                || (organization.RetentionDays > 0 && eventAgeInDays > organization.RetentionDays);
        });

        if (!organization.HasPremiumFeatures)
            return discarded;

        foreach (var routeGroup in uniqueCandidates
            .Where(candidate => candidate.Route is { Status: StackStatus.Fixed, DateFixed: not null, FixedInVersion: not null })
            .GroupBy(candidate => candidate.Fingerprint.SignatureHash)
            .ToArray())
        {
            StackRoute route = routeGroup.First().Route!;
            var fixedVersion = semanticVersionParser.Parse(route.FixedInVersion);
            if (fixedVersion is null)
                continue;

            foreach (Candidate candidate in routeGroup.ToArray())
            {
                var version = semanticVersionParser.Parse(candidate.Source.Version) ?? semanticVersionParser.Default;
                if (version >= fixedVersion)
                    continue;

                if (uniqueCandidates.Remove(candidate))
                    discarded++;
            }
        }

        return discarded;
    }

    private readonly struct SourceEvent
    {
        private readonly EventIngestionV3BufferedRecord? _bufferedRecord;

        public SourceEvent(EventIngestionV3Event source)
        {
            RoutingEvent = source;
        }

        public SourceEvent(EventIngestionV3BufferedRecord bufferedRecord)
        {
            _bufferedRecord = bufferedRecord;
            RoutingEvent = bufferedRecord.RoutingEvent;
        }

        public EventIngestionV3Event RoutingEvent { get; }

        public EventIngestionV3Event Materialize() => _bufferedRecord?.Materialize() ?? RoutingEvent;
    }

    private sealed class Candidate(SourceEvent source, StackFingerprint fingerprint)
    {
        private readonly SourceEvent _source = source;

        public EventIngestionV3Event Source { get; private set; } = source.RoutingEvent;
        public StackFingerprint Fingerprint { get; private set; } = fingerprint;
        public StackRoute? Route { get; set; }
        public EventIngestionIdentity? Identity { get; set; }
        public bool IsRegressionCandidate { get; set; }

        public void Materialize()
        {
            Source = _source.Materialize();
            if (Source.Stacking is not null)
                Fingerprint = Fingerprint with { Title = Source.Stacking.Title };
        }
    }
}
