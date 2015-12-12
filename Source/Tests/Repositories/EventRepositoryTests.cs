using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Helpers;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class EventRepositoryTests {
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IEventRepository _repository = IoC.GetInstance<IEventRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();


        [Fact]
        public async Task GetAsync() {
            await RemoveDataAsync();

            var ev = await _repository.AddAsync(new RandomEventGenerator().GeneratePersistent());
            var result = await _repository.GetByIdAsync(ev.Id);
            Assert.NotNull(result);
        }

        [Fact(Skip="Performance Testing")]
        public async Task GetAsyncPerformance() {
            await RemoveDataAsync();
            
            var ev = await _repository.AddAsync(new RandomEventGenerator().GeneratePersistent());
            await _client.RefreshAsync();
            Assert.Equal(1, await _repository.CountAsync());

            var sw = Stopwatch.StartNew();
            const int MAX_ITERATIONS = 100;
            for (int i = 0; i < MAX_ITERATIONS; i++) {
                Assert.NotNull(await _repository.GetByIdAsync(ev.Id));
            }

            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        [Fact]
        public async Task GetPagedAsync() {
            await RemoveDataAsync();

            var events = new List<PersistentEvent>();
            for (int i = 0; i < 6; i++)
                events.Add(EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: DateTime.Now.Subtract(TimeSpan.FromMinutes(i))));

            await _repository.AddAsync(events);
            await _client.RefreshAsync();
            Assert.Equal(events.Count, await _repository.CountAsync());

            var results = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithPage(2).WithLimit(2));
            Assert.Equal(2, results.Documents.Count);
            Assert.Equal(results.Documents.First().Id, events[2].Id);
            Assert.Equal(results.Documents.Last().Id, events[3].Id);

            results = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithPage(3).WithLimit(2));
            Assert.Equal(2, results.Documents.Count);
            Assert.Equal(results.Documents.First().Id, events[4].Id);
            Assert.Equal(results.Documents.Last().Id, events[5].Id);
        }

        [Fact]
        public async Task GetByQueryAsync() {
            await RemoveDataAsync();
            await CreateDataAsync();

            Debug.WriteLine("Sorted order:");
            List<Tuple<string, DateTime>> sortedIds = _ids.OrderByDescending(t => t.Item2.Ticks).ThenByDescending(t => t.Item1).ToList();
            foreach (var t in sortedIds)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.ToLongTimeString());
            
            Debug.WriteLine("");
            Debug.WriteLine("Before {0}: {1}", sortedIds[2].Item1, sortedIds[2].Item2.ToLongTimeString());
            await _client.RefreshAsync();
            string query = $"stack:{TestConstants.StackId} project:{TestConstants.ProjectId} date:[now-1h TO now+1h]";
            var results = (await _repository.GetByOrganizationIdsAsync(new[] { TestConstants.OrganizationId }, query, new PagingOptions().WithLimit(20))).Documents.ToArray();
            Assert.Equal(sortedIds.Count, results.Length);

            for (int i = 0; i < sortedIds.Count; i++) {
                Debug.WriteLine("{0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
                Assert.Equal(sortedIds[i].Item1, results[i].Id);
            }
        }
       
        [Fact]
        public async Task GetPreviousEventIdInStackTestAsync() {
            await RemoveDataAsync();
            await CreateDataAsync();

            Debug.WriteLine("Actual order:");
            foreach (var t in _ids)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Sorted order:");
            List<Tuple<string, DateTime>> sortedIds = _ids.OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1).ToList();
            foreach (var t in sortedIds)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Tests:");
            await _client.RefreshAsync();
            Assert.Equal(_ids.Count, await _repository.CountAsync());
            for (int i = 0; i < sortedIds.Count; i++) {
                Debug.WriteLine("Current - {0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
                if (i == 0)
                    Assert.Null((await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[i].Item1)).Previous);
                else
                    Assert.Equal(sortedIds[i - 1].Item1, (await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[i].Item1)).Previous);
            }
        }

        [Fact]
        public async Task GetNextEventIdInStackTestAsync() {
            await RemoveDataAsync();
            await CreateDataAsync();

            Debug.WriteLine("Actual order:");
            foreach (var t in _ids)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Sorted order:");
            List<Tuple<string, DateTime>> sortedIds = _ids.OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1).ToList();
            foreach (var t in sortedIds)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Tests:");
            await _client.RefreshAsync();
            Assert.Equal(_ids.Count, await _repository.CountAsync());
            for (int i = 0; i < sortedIds.Count; i++) {
                Debug.WriteLine("Current - {0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
                string nextId = (await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[i].Item1)).Next;
                if (i == sortedIds.Count - 1)
                    Assert.Null(nextId);
                else
                    Assert.Equal(sortedIds[i + 1].Item1, nextId);
            }
        }

        [Fact]
        public async Task GetByReferenceIdAsync() {
            await RemoveDataAsync();

            string referenceId = ObjectId.GenerateNewId().ToString();
            await _repository.AddAsync(EventData.GenerateEvents(count: 3, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, referenceId: referenceId).ToList());

            await _client.RefreshAsync();
            var results = await _repository.GetByReferenceIdAsync(TestConstants.ProjectId, referenceId);
            Assert.True(results.Total > 0);
            Assert.NotNull(results.Documents.First());
            Assert.Equal(referenceId, results.Documents.First().ReferenceId);
        }

        [Fact]
        public async Task MarkAsFixedByStackTestAsync() {
            await RemoveDataAsync();

            const int NUMBER_OF_EVENTS_TO_CREATE = 50;
            await _repository.AddAsync(EventData.GenerateEvents(count: NUMBER_OF_EVENTS_TO_CREATE, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, isFixed: true).ToList());

            await _client.RefreshAsync();
            Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, await _repository.CountAsync());
            
            await _repository.UpdateFixedByStackAsync(TestConstants.OrganizationId, TestConstants.StackId2, false);

            await _client.RefreshAsync();
            var events = await _repository.GetByStackIdAsync(TestConstants.StackId2, new PagingOptions().WithLimit(NUMBER_OF_EVENTS_TO_CREATE));
            Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, events.Total);
            foreach (var ev in events.Documents)
                Assert.False(ev.IsFixed);
        }

        [Fact(Skip = "TODO")]
        public Task RemoveOldestEventsTestAsync() {
            return Task.CompletedTask;
        }

        [Fact(Skip = "TODO")]
        public Task RemoveAllByDateTestAsync() {
            return Task.CompletedTask;
        }
        
        [Fact]
        public async Task RemoveAllByClientIpAndDateAsync() {
            await RemoveDataAsync();
            const string _clientIpAddress = "123.123.12.256";

            const int NUMBER_OF_EVENTS_TO_CREATE = 50;
            var events = EventData.GenerateEvents(count: NUMBER_OF_EVENTS_TO_CREATE, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, isFixed: true, startDate: DateTime.Now.SubtractDays(2), endDate: DateTime.Now).ToList();
            events.ForEach(e => e.AddRequestInfo(new RequestInfo { ClientIpAddress = _clientIpAddress }));
            await _repository.AddAsync(events);

            await _client.RefreshAsync();
            events = (await _repository.GetByStackIdAsync(TestConstants.StackId2, new PagingOptions().WithLimit(NUMBER_OF_EVENTS_TO_CREATE))).Documents.ToList();
            Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, events.Count);
            events.ForEach(e => {
                Assert.False(e.IsHidden);
                var ri = e.GetRequestInfo();
                Assert.NotNull(ri);
                Assert.Equal(_clientIpAddress, ri.ClientIpAddress);
            });

            await _repository.HideAllByClientIpAndDateAsync(TestConstants.OrganizationId, _clientIpAddress, DateTime.UtcNow.SubtractDays(3), DateTime.UtcNow.AddDays(2));

            await _client.RefreshAsync();
            events = (await _repository.GetByStackIdAsync(TestConstants.StackId2, new PagingOptions().WithLimit(NUMBER_OF_EVENTS_TO_CREATE))).Documents.ToList();
            Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, events.Count);
            events.ForEach(e => Assert.True(e.IsHidden));
        }

        private readonly List<Tuple<string, DateTime>> _ids = new List<Tuple<string, DateTime>>();

        protected async Task CreateDataAsync() {
            var baseDate = DateTime.UtcNow;
            var occurrenceDateStart = baseDate.AddMinutes(-30);
            var occurrenceDateMid = baseDate;
            var occurrenceDateEnd = baseDate.AddMinutes(30);

            await _stackRepository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId));

            var occurrenceDates = new List<DateTime> {
                occurrenceDateStart,
                occurrenceDateEnd,
                baseDate.AddMinutes(-10),
                baseDate.AddMinutes(-20),
                occurrenceDateMid,
                occurrenceDateMid,
                occurrenceDateMid,
                baseDate.AddMinutes(20),
                baseDate.AddMinutes(10),
                baseDate.AddSeconds(1),
                occurrenceDateEnd,
                occurrenceDateStart
            };

            foreach (var date in occurrenceDates) {
                var ev = await _repository.AddAsync(EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: date));
                _ids.Add(Tuple.Create(ev.Id, date));
            }
        }

        protected async Task RemoveDataAsync() {
            await _repository.RemoveAllAsync();
            await _client.RefreshAsync();
            await _stackRepository.RemoveAllAsync();
            await _client.RefreshAsync();
        }
    }
}