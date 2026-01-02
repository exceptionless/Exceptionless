using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public class SetStackDuplicateSignatureMigrationTests : IntegrationTestsBase
{
    private readonly StackData _stackData;
    private readonly IStackRepository _repository;

    public SetStackDuplicateSignatureMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _stackData = GetService<StackData>();
        _repository = GetService<IStackRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<SetStackDuplicateSignature>();
        services.AddSingleton<ILock>(new EmptyLock());
        base.RegisterServices(services);
    }

    [Fact]
    public async Task WillSetStackDuplicateSignature()
    {
        var stack = _stackData.GenerateStack();
        stack.DuplicateSignature = null!;
        stack = await _repository.AddAsync(stack, o => o.ImmediateConsistency());
        Assert.NotEmpty(stack.ProjectId);
        Assert.NotEmpty(stack.SignatureHash);
        Assert.Null(stack.DuplicateSignature);

        var migration = GetService<SetStackDuplicateSignature>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);
        await RefreshDataAsync();

        string expectedDuplicateSignature = $"{stack.ProjectId}:{stack.SignatureHash}";
        var actualStack = await _repository.GetByIdAsync(stack.Id);
        Assert.NotEmpty(actualStack.ProjectId);
        Assert.NotEmpty(actualStack.SignatureHash);
        Assert.Equal($"{actualStack.ProjectId}:{actualStack.SignatureHash}", actualStack.DuplicateSignature);

        var results = await _repository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, expectedDuplicateSignature)));
        Assert.Single(results.Documents);
    }
}
