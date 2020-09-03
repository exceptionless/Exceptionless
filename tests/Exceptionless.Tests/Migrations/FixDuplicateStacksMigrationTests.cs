using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Migrations {
    public class FixDuplicateStacksMigrationTests : IntegrationTestsBase {
        private readonly IStackRepository _repository;
        
        public FixDuplicateStacksMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _repository = GetService<IStackRepository>();
        }

        protected override void RegisterServices(IServiceCollection services) {
            services.AddTransient<SetStackDuplicateSignature>();
            services.AddSingleton<ILock>(new EmptyLock());
            base.RegisterServices(services);
        }

        [Fact]
        public async Task WillMergeDuplicatedStacks() {
            var stack1 = StackData.GenerateStack();
            stack1.Id = ObjectId.GenerateNewId().ToString();
            var stack2 = stack1.DeepClone();
            stack2.Id = ObjectId.GenerateNewId().ToString();
            stack2.CreatedUtc = stack1.CreatedUtc.AddMinutes(1);
            stack2.Status = StackStatus.Fixed;
            stack2.LastOccurrence = stack1.LastOccurrence.AddMinutes(1);
            stack2.SnoozeUntilUtc = stack1.SnoozeUntilUtc = null;
            stack2.DateFixed = stack2.LastOccurrence.AddMinutes(1);
            stack2.UpdatedUtc = stack1.UpdatedUtc.SubtractMinutes(1);
            stack2.Tags.Add("stack2");
            stack2.References.Add("stack2");
            stack2.OccurrencesAreCritical = true;
            
            await _repository.AddAsync(new []{ stack1, stack2 }, o => o.ImmediateConsistency());
              
            var results = await _repository.GetStackByDuplicateSignatureAsync(stack1.DuplicateSignature);
            Assert.Equal(2, results.Total);
            
            var migration = GetService<FixDuplicateStacks>();
            var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
            await migration.RunAsync(context);

            await RefreshDataAsync();

            results = await _repository.GetStackByDuplicateSignatureAsync(stack1.DuplicateSignature);
            Assert.Single(results.Documents);

            var actualStack1 = await _repository.GetByIdAsync(stack1.Id, o => o.IncludeSoftDeletes());
            Assert.False(actualStack1.IsDeleted);
            var actualStack2 = await _repository.GetByIdAsync(stack2.Id, o => o.IncludeSoftDeletes());
            Assert.True(actualStack2.IsDeleted);
            
            Assert.Equal(stack1.CreatedUtc, actualStack1.CreatedUtc);
            Assert.Equal(stack2.Status, actualStack1.Status);
            Assert.Equal(stack2.LastOccurrence, actualStack1.LastOccurrence);
            Assert.Equal(stack1.SnoozeUntilUtc, actualStack1.SnoozeUntilUtc);
            Assert.Equal(stack2.DateFixed, actualStack1.DateFixed);
            Assert.Equal(stack1.UpdatedUtc, actualStack1.UpdatedUtc);
            Assert.Equal(stack1.Tags.Count + 1, actualStack1.Tags.Count);
            Assert.Contains("stack2", actualStack1.Tags);
            Assert.Equal(stack1.References.Count + 1, actualStack1.References.Count);
            Assert.Contains("stack2", actualStack1.References);
            Assert.True(actualStack1.OccurrencesAreCritical);
        }
        
        [Fact]
        public async Task WillNotMergeDuplicatedDeletedStacks() {
            var stack1 = StackData.GenerateStack();
            var stack2 = stack1.DeepClone();
            stack2.Id = ObjectId.GenerateNewId().ToString();
            stack2.CreatedUtc = stack1.CreatedUtc.AddMinutes(1);
            stack2.Status = StackStatus.Fixed;
            stack2.LastOccurrence = stack1.LastOccurrence.AddMinutes(1);
            stack2.SnoozeUntilUtc = stack1.SnoozeUntilUtc = null;
            stack2.DateFixed = stack2.LastOccurrence.AddMinutes(1);
            stack2.UpdatedUtc = stack1.UpdatedUtc.SubtractMinutes(1);
            stack2.Tags.Add("stack2");
            stack2.References.Add("stack2");
            stack2.OccurrencesAreCritical = true;
            stack2.IsDeleted = true;
            
            await _repository.AddAsync(new []{ stack1, stack2 }, o => o.ImmediateConsistency());
              
            var results = await _repository.GetStackByDuplicateSignatureAsync(stack1.DuplicateSignature);
            Assert.Single(results.Documents);
            
            var migration = GetService<FixDuplicateStacks>();
            var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
            await migration.RunAsync(context);

            await RefreshDataAsync();

            results = await _repository.GetStackByDuplicateSignatureAsync(stack1.DuplicateSignature);
            Assert.Single(results.Documents);

            var actualStack1 = await _repository.GetByIdAsync(stack1.Id, o => o.IncludeSoftDeletes());
            Assert.False(actualStack1.IsDeleted);
            var actualStack2 = await _repository.GetByIdAsync(stack2.Id, o => o.IncludeSoftDeletes());
            Assert.True(actualStack2.IsDeleted);
            
            Assert.Equal(stack1.CreatedUtc, actualStack1.CreatedUtc);
            Assert.Equal(stack1.Status, actualStack1.Status);
            Assert.Equal(stack1.LastOccurrence, actualStack1.LastOccurrence);
            Assert.Equal(stack1.SnoozeUntilUtc, actualStack1.SnoozeUntilUtc);
            Assert.Equal(stack1.DateFixed, actualStack1.DateFixed);
            Assert.Equal(stack1.UpdatedUtc, actualStack1.UpdatedUtc);
            Assert.Equal(stack1.Tags.Count, actualStack1.Tags.Count);
            Assert.DoesNotContain("stack2", actualStack1.Tags);
            Assert.Equal(stack1.References.Count , actualStack1.References.Count);
            Assert.DoesNotContain("stack2", actualStack1.References);
            Assert.False(actualStack1.OccurrencesAreCritical);
        }
    }
}