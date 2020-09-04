using System.Threading;
using System.Threading.Tasks;
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
using Nest;
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
            var utcNow = SystemClock.UtcNow;
            var originalStack = StackData.GenerateStack();
            originalStack.Id = ObjectId.GenerateNewId().ToString();
            originalStack.TotalOccurrences = 10;
            var duplicateStack = originalStack.DeepClone();
            duplicateStack.Id = ObjectId.GenerateNewId().ToString();
            duplicateStack.Status = StackStatus.Fixed;
            duplicateStack.TotalOccurrences = 100;
            duplicateStack.LastOccurrence = originalStack.LastOccurrence.AddMinutes(1);
            duplicateStack.SnoozeUntilUtc = originalStack.SnoozeUntilUtc = null;
            duplicateStack.DateFixed = duplicateStack.LastOccurrence.AddMinutes(1);
            duplicateStack.Tags.Add("stack2");
            duplicateStack.References.Add("stack2");
            duplicateStack.OccurrencesAreCritical = true;

            originalStack = await _repository.AddAsync(originalStack, o => o.ImmediateConsistency());
            duplicateStack = await _repository.AddAsync(duplicateStack, o => o.ImmediateConsistency());

            var results = await _repository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Equal(2, results.Total);
            
            var migration = GetService<FixDuplicateStacks>();
            var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
            await migration.RunAsync(context);

            await RefreshDataAsync();

            results = await _repository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Single(results.Documents);

            var updatedOriginalStack = await _repository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
            Assert.False(originalStack.IsDeleted);
            var updatedDuplicateStack = await _repository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());
            Assert.True(updatedDuplicateStack.IsDeleted);
            
            Assert.Equal(originalStack.CreatedUtc, updatedOriginalStack.CreatedUtc);
            Assert.Equal(110, updatedOriginalStack.TotalOccurrences);
            Assert.Equal(StackStatus.Fixed, updatedOriginalStack.Status);
            Assert.Equal(duplicateStack.LastOccurrence, updatedOriginalStack.LastOccurrence);
            Assert.Null(updatedOriginalStack.SnoozeUntilUtc);
            Assert.Equal(duplicateStack.DateFixed, updatedOriginalStack.DateFixed);
            Assert.Equal(originalStack.Tags.Count + 1, updatedOriginalStack.Tags.Count);
            Assert.Contains("stack2", updatedOriginalStack.Tags);
            Assert.Equal(originalStack.References.Count + 1, updatedOriginalStack.References.Count);
            Assert.Contains("stack2", updatedOriginalStack.References);
            Assert.True(updatedOriginalStack.OccurrencesAreCritical);
        }
        
        [Fact]
        public async Task WillNotMergeDuplicatedDeletedStacks() {
            var originalStack = StackData.GenerateStack();
            var duplicateStack = originalStack.DeepClone();
            duplicateStack.Id = ObjectId.GenerateNewId().ToString();
            duplicateStack.CreatedUtc = originalStack.CreatedUtc.AddMinutes(1);
            duplicateStack.Status = StackStatus.Fixed;
            duplicateStack.LastOccurrence = originalStack.LastOccurrence.AddMinutes(1);
            duplicateStack.SnoozeUntilUtc = originalStack.SnoozeUntilUtc = null;
            duplicateStack.DateFixed = duplicateStack.LastOccurrence.AddMinutes(1);
            duplicateStack.UpdatedUtc = originalStack.UpdatedUtc.SubtractMinutes(1);
            duplicateStack.Tags.Add("stack2");
            duplicateStack.References.Add("stack2");
            duplicateStack.OccurrencesAreCritical = true;
            duplicateStack.IsDeleted = true;
            
            await _repository.AddAsync(new []{ originalStack, duplicateStack }, o => o.ImmediateConsistency());
              
            var results = await _repository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Single(results.Documents);
            
            var migration = GetService<FixDuplicateStacks>();
            var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
            await migration.RunAsync(context);

            await RefreshDataAsync();

            results = await _repository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Single(results.Documents);

            var updatedOriginalStack = await _repository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
            Assert.False(updatedOriginalStack.IsDeleted);
            var updatedDuplicateStack = await _repository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());
            Assert.True(updatedDuplicateStack.IsDeleted);
            
            Assert.Equal(originalStack.CreatedUtc, updatedOriginalStack.CreatedUtc);
            Assert.Equal(originalStack.Status, updatedOriginalStack.Status);
            Assert.Equal(originalStack.LastOccurrence, updatedOriginalStack.LastOccurrence);
            Assert.Equal(originalStack.SnoozeUntilUtc, updatedOriginalStack.SnoozeUntilUtc);
            Assert.Equal(originalStack.DateFixed, updatedOriginalStack.DateFixed);
            Assert.Equal(originalStack.UpdatedUtc, updatedOriginalStack.UpdatedUtc);
            Assert.Equal(originalStack.Tags.Count, updatedOriginalStack.Tags.Count);
            Assert.DoesNotContain("stack2", updatedOriginalStack.Tags);
            Assert.Equal(originalStack.References.Count , updatedOriginalStack.References.Count);
            Assert.DoesNotContain("stack2", updatedOriginalStack.References);
            Assert.False(updatedOriginalStack.OccurrencesAreCritical);
        }
    }
}