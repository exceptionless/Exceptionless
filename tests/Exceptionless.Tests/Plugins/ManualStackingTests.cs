using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Tests.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Plugins;

public class ManualStackingTests : TestWithServices
{
    private readonly OrganizationData _organizationData;
    private readonly ProjectData _projectData;

    public ManualStackingTests(ITestOutputHelper output) : base(output)
    {
        _organizationData = GetService<OrganizationData>();
        _projectData = GetService<ProjectData>();
    }

    [Theory]
    [MemberData(nameof(StackingData))]
    public async Task AddManualStackSignatureData(string stackingKey, bool willAddManualStackSignature)
    {
        var ev = new PersistentEvent();
        ev.SetManualStackingKey(stackingKey);

        var context = new EventContext(ev, _organizationData.GenerateSampleOrganization(GetService<BillingManager>(), GetService<BillingPlans>()), _projectData.GenerateSampleProject());
        var plugin = GetService<ManualStackingPlugin>();
        await plugin.EventBatchProcessingAsync(new List<EventContext> { context });
        Assert.Equal(willAddManualStackSignature, context.StackSignatureData.Count > 0);
    }

    public static IEnumerable<object?[]> StackingData => new List<object?[]> {
            new object?[] { "ManualStackData", true },
            new object?[] { null, false },
            new object?[] { String.Empty, false }
        }.ToArray();
}
