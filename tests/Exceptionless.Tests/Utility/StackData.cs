using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class StackData
{
    private readonly TimeProvider _timeProvider;
    private readonly ITextSerializer _serializer;
    private readonly IStackRepository _stackRepository;

    public StackData(IStackRepository stackRepository, ITextSerializer serializer, TimeProvider timeProvider)
    {
        _stackRepository = stackRepository;
        _serializer = serializer;
        _timeProvider = timeProvider;
    }

    public IEnumerable<Stack> GenerateStacks(int count = 10, bool generateId = false, string? id = null, string? organizationId = null, string? projectId = null, string? type = null)
    {
        for (int i = 0; i < count; i++)
            yield return GenerateStack(generateId, id, organizationId, projectId, type: type);
    }

    public List<Stack> GenerateSampleStacks()
    {
        return
        [
            GenerateSampleStack(),
            GenerateStack(id: TestConstants.StackId2, organizationId: TestConstants.OrganizationId,
                projectId: TestConstants.ProjectIdWithNoRoles),
            GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId)
        ];
    }

    public Stack GenerateSampleStack(string id = TestConstants.StackId)
    {
        return GenerateStack(id: id, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId);
    }

    public Stack GenerateStack(bool generateId = false, string? id = null, string? organizationId = null, string? projectId = null, string? type = null, string? title = null, DateTime? dateFixed = null, DateTime? utcFirstOccurrence = null, DateTime? utcLastOccurrence = null, int totalOccurrences = 0, StackStatus status = StackStatus.Open, string? signatureHash = null)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var stack = new Stack
        {
            Id = (id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : null : id)!,
            OrganizationId = organizationId.IsNullOrEmpty() ? TestConstants.OrganizationId : organizationId,
            ProjectId = (projectId.IsNullOrEmpty() ? TestConstants.ProjectIds.Random() : projectId)!,
            Title = title ?? RandomData.GetTitleWords(),
            Type = type ?? Stack.KnownTypes.Error,
            DateFixed = dateFixed,
            FirstOccurrence = utcFirstOccurrence ?? utcNow,
            LastOccurrence = utcLastOccurrence ?? utcNow,
            TotalOccurrences = totalOccurrences,
            Status = status,
            SignatureHash = signatureHash ?? RandomData.GetAlphaNumericString(10, 10),
            SignatureInfo = new SettingsDictionary()
        };

        stack.DuplicateSignature = String.Concat(stack.ProjectId, ":", stack.SignatureHash);

        if (type == Event.KnownTypes.Error)
            stack.SignatureInfo.Add("ExceptionType", TestConstants.ExceptionTypes.Random()!);

        for (int i = 0; i < RandomData.GetInt(0, 5); i++)
        {
            string tag = RandomData.GetWord();
            while (stack.Tags.Contains(tag))
                tag = RandomData.GetWord();

            stack.Tags.Add(tag);
        }

        return stack;
    }

    public async Task CreateSearchDataAsync(bool updateDates = false)
    {
        string path = Path.Combine("..", "..", "..", "Search", "Data");
        foreach (string file in Directory.GetFiles(path, "stack*.json", SearchOption.AllDirectories))
        {
            if (file.EndsWith("summary.json"))
                continue;

            await using var stream = new FileStream(file, FileMode.Open);
            var stack = _serializer.Deserialize<Stack>(stream);
            Assert.NotNull(stack);

            if (updateDates)
            {
                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
                stack.CreatedUtc = stack.FirstOccurrence = utcNow.SubtractDays(1);
                stack.LastOccurrence = utcNow;
            }

            await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());
        }
    }

}
