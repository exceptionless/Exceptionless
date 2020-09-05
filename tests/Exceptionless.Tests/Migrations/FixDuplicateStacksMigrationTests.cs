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
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;

        public FixDuplicateStacksMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _stackRepository = GetService<IStackRepository>();
            _eventRepository = GetService<IEventRepository>();
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
            originalStack.TotalOccurrences = 100;
            var duplicateStack = originalStack.DeepClone();
            duplicateStack.Id = ObjectId.GenerateNewId().ToString();
            duplicateStack.Status = StackStatus.Fixed;
            duplicateStack.TotalOccurrences = 10;
            duplicateStack.LastOccurrence = originalStack.LastOccurrence.AddMinutes(1);
            duplicateStack.SnoozeUntilUtc = originalStack.SnoozeUntilUtc = null;
            duplicateStack.DateFixed = duplicateStack.LastOccurrence.AddMinutes(1);
            duplicateStack.Tags.Add("stack2");
            duplicateStack.References.Add("stack2");
            duplicateStack.OccurrencesAreCritical = true;

            originalStack = await _stackRepository.AddAsync(originalStack, o => o.ImmediateConsistency());
            duplicateStack = await _stackRepository.AddAsync(duplicateStack, o => o.ImmediateConsistency());

            await _eventRepository.AddAsync(EventData.GenerateEvents(count: 100, stackId: originalStack.Id), o => o.ImmediateConsistency());
            await _eventRepository.AddAsync(EventData.GenerateEvents(count: 10, stackId: duplicateStack.Id), o => o.ImmediateConsistency());

            var results = await _stackRepository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Equal(2, results.Total);
            
            var migration = GetService<FixDuplicateStacks>();
            var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
            await migration.RunAsync(context);

            await RefreshDataAsync();

            results = await _stackRepository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Single(results.Documents);

            var updatedOriginalStack = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
            Assert.False(updatedOriginalStack.IsDeleted);
            var updatedDuplicateStack = await _stackRepository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());
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
        public async Task WillMergeToStackWithMostEvents() {
            var utcNow = SystemClock.UtcNow;
            var originalStack = StackData.GenerateStack();
            originalStack.Id = ObjectId.GenerateNewId().ToString();
            originalStack.TotalOccurrences = 10;
            var biggerStack = originalStack.DeepClone();
            biggerStack.Id = ObjectId.GenerateNewId().ToString();
            biggerStack.Status = StackStatus.Fixed;
            biggerStack.TotalOccurrences = 100;
            biggerStack.LastOccurrence = originalStack.LastOccurrence.AddMinutes(1);
            biggerStack.SnoozeUntilUtc = originalStack.SnoozeUntilUtc = null;
            biggerStack.DateFixed = biggerStack.LastOccurrence.AddMinutes(1);
            biggerStack.Tags.Add("stack2");
            biggerStack.References.Add("stack2");
            biggerStack.OccurrencesAreCritical = true;

            originalStack = await _stackRepository.AddAsync(originalStack, o => o.ImmediateConsistency());
            biggerStack = await _stackRepository.AddAsync(biggerStack, o => o.ImmediateConsistency());

            await _eventRepository.AddAsync(EventData.GenerateEvents(count: 10, stackId: originalStack.Id), o => o.ImmediateConsistency());
            await _eventRepository.AddAsync(EventData.GenerateEvents(count: 100, stackId: biggerStack.Id), o => o.ImmediateConsistency());

            var results = await _stackRepository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Equal(2, results.Total);

            var migration = GetService<FixDuplicateStacks>();
            var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
            await migration.RunAsync(context);

            await RefreshDataAsync();

            results = await _stackRepository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Single(results.Documents);

            var updatedOriginalStack = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
            Assert.True(updatedOriginalStack.IsDeleted);
            var updatedBiggerStack = await _stackRepository.GetByIdAsync(biggerStack.Id, o => o.IncludeSoftDeletes());
            Assert.False(updatedBiggerStack.IsDeleted);

            Assert.Equal(originalStack.CreatedUtc, updatedBiggerStack.CreatedUtc);
            Assert.Equal(110, updatedBiggerStack.TotalOccurrences);
            Assert.Equal(StackStatus.Fixed, updatedBiggerStack.Status);
            Assert.Equal(biggerStack.LastOccurrence, updatedBiggerStack.LastOccurrence);
            Assert.Null(updatedBiggerStack.SnoozeUntilUtc);
            Assert.Equal(biggerStack.DateFixed, updatedBiggerStack.DateFixed);
            Assert.Equal(originalStack.Tags.Count + 1, updatedBiggerStack.Tags.Count);
            Assert.Contains("stack2", updatedBiggerStack.Tags);
            Assert.Equal(originalStack.References.Count + 1, updatedBiggerStack.References.Count);
            Assert.Contains("stack2", updatedBiggerStack.References);
            Assert.True(updatedBiggerStack.OccurrencesAreCritical);
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
            
            await _stackRepository.AddAsync(new []{ originalStack, duplicateStack }, o => o.ImmediateConsistency());
              
            var results = await _stackRepository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Single(results.Documents);
            
            var migration = GetService<FixDuplicateStacks>();
            var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
            await migration.RunAsync(context);

            await RefreshDataAsync();

            results = await _stackRepository.FindAsync(q => q.ElasticFilter(Query<Stack>.Term(s => s.DuplicateSignature, originalStack.DuplicateSignature)));
            Assert.Single(results.Documents);

            var updatedOriginalStack = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
            Assert.False(updatedOriginalStack.IsDeleted);
            var updatedDuplicateStack = await _stackRepository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());
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