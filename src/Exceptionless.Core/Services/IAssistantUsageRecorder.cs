using Exceptionless.Core.Models;

namespace Exceptionless.Core.Services;

public interface IAssistantUsageRecorder
{
    Task RecordAssistantUsageAsync(string organizationId, AssistantUsageIncrement increment);
}
