using System.Collections.Concurrent;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;

namespace Exceptionless.Tests.Assistant;

internal sealed class RecordingAssistantUsageRecorder : IAssistantUsageRecorder
{
    private readonly ConcurrentQueue<(string OrganizationId, AssistantUsageIncrement Increment)> _records = new();

    public IReadOnlyCollection<(string OrganizationId, AssistantUsageIncrement Increment)> Records => _records.ToArray();
    public Exception? Exception { get; set; }

    public Task RecordAssistantUsageAsync(string organizationId, AssistantUsageIncrement increment)
    {
        if (Exception is not null)
            return Task.FromException(Exception);

        _records.Enqueue((organizationId, increment));
        return Task.CompletedTask;
    }
}
