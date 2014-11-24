using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Tests.Utility;
using MongoDB.Bson;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class EventRepositoryTests {
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IEventRepository _repository = IoC.GetInstance<IEventRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        
        [Fact]
        public void GetPaged() {
            RemoveData();

            var events = new List<PersistentEvent>();
            for (int i = 0; i < 6; i++)
                events.Add(EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: DateTime.Now.Subtract(TimeSpan.FromMinutes(i))));

            _repository.Add(events);
            _client.Refresh(r => r.Force(false));
            Assert.Equal(events.Count, _repository.Count());

            var results = _repository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithPage(2).WithLimit(2));
            Assert.Equal(2, results.Count);
            Assert.Equal(results.First().Id, events[2].Id);
            Assert.Equal(results.Last().Id, events[3].Id);

            results = _repository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithPage(3).WithLimit(2));
            Assert.Equal(2, results.Count);
            Assert.Equal(results.First().Id, events[4].Id);
            Assert.Equal(results.Last().Id, events[5].Id);
        }

        [Fact]
        public void GetByQuery() {
            RemoveData();
            CreateData();

            Debug.WriteLine("Sorted order:");
            List<Tuple<string, DateTime>> sortedIds = _ids.OrderByDescending(t => t.Item2.Ticks).ThenByDescending(t => t.Item1).ToList();
            foreach (var t in sortedIds)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Before {0}: {1}", sortedIds[2].Item1, sortedIds[2].Item2.ToLongTimeString());
            _client.Refresh(r => r.Force(false));
            string query = String.Format("stack:{0} project:{1} date:[now-1h TO now+1h]", TestConstants.StackId, TestConstants.ProjectId);
            var results = _repository.GetByOrganizationIds(new[] { TestConstants.OrganizationId }, query, new PagingOptions().WithLimit(20)).ToArray();
            Assert.True(results.Length > 0);

            for (int i = 0; i < sortedIds.Count; i++) {
                Debug.WriteLine("{0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
                Assert.Equal(sortedIds[i].Item1, results[i].Id);
            }
        }
       
        [Fact]
        public void GetPreviousEventIdInStackTest() {
            RemoveData();
            CreateData();

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
            _client.Refresh(r => r.Force(false));
            Assert.Equal(_ids.Count, _repository.Count());
            for (int i = 0; i < sortedIds.Count; i++) {
                Debug.WriteLine("Current - {0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
                if (i == 0)
                    Assert.Null(_repository.GetPreviousEventId(sortedIds[i].Item1));
                else
                    Assert.Equal(sortedIds[i - 1].Item1, _repository.GetPreviousEventId(sortedIds[i].Item1));
            }
        }

        [Fact]
        public void GetNextEventIdInStackTest() {
            RemoveData();
            CreateData();

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
            _client.Refresh(r => r.Force(false));
            Assert.Equal(_ids.Count, _repository.Count());
            for (int i = 0; i < sortedIds.Count; i++) {
                Debug.WriteLine("Current - {0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
                string nextId = _repository.GetNextEventId(sortedIds[i].Item1);
                if (i == sortedIds.Count - 1)
                    Assert.Null(nextId);
                else
                    Assert.Equal(sortedIds[i + 1].Item1, nextId);
            }
        }

        [Fact]
        public void GetByReferenceId() {
            RemoveData();

            string referenceId = ObjectId.GenerateNewId().ToString();
            _repository.Add(EventData.GenerateEvents(count: 3, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, referenceId: referenceId).ToList());

            _client.Refresh();
            var results = _repository.GetByReferenceId(TestConstants.ProjectId, referenceId);
            Assert.True(results.Count > 0);
            Assert.NotNull(results.FirstOrDefault());
            Assert.Equal(referenceId, results.FirstOrDefault().ReferenceId);
        }

        [Fact]
        public void MarkAsFixedByStackTest() {
            RemoveData();

            const int NUMBER_OF_EVENTS_TO_CREATE = 50;
            _repository.Add(EventData.GenerateEvents(count: NUMBER_OF_EVENTS_TO_CREATE, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, isFixed: true).ToList());

            _client.Refresh();
            Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, _repository.Count());
            
            _repository.MarkAsRegressedByStack(TestConstants.OrganizationId, TestConstants.StackId2);

            _client.Refresh();
            var events = _repository.GetByStackId(TestConstants.StackId2, new PagingOptions().WithLimit(NUMBER_OF_EVENTS_TO_CREATE));
            Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, events.Count);
            foreach (var ev in events)
                Assert.False(ev.IsFixed);
        }
        
        [Fact(Skip = "TODO")]
        public void RemoveOldestEventsTest() { }

        [Fact(Skip = "TODO")]
        public void RemoveAllByDateTest() { }
        
        [Fact]
        public void RemoveAllByClientIpAndDate() {
            RemoveData();
            const string _clientIpAddress = "123.123.12.256";

            const int NUMBER_OF_EVENTS_TO_CREATE = 50;
            var events = EventData.GenerateEvents(count: NUMBER_OF_EVENTS_TO_CREATE, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId2, isFixed: true, startDate: DateTime.Now.SubtractDays(2), endDate: DateTime.Now).ToList();
            events.ForEach(e => e.AddRequestInfo(new RequestInfo { ClientIpAddress = _clientIpAddress }));
            _repository.Add(events);

            _client.Refresh();
            events = _repository.GetByStackId(TestConstants.StackId2, new PagingOptions().WithLimit(NUMBER_OF_EVENTS_TO_CREATE)).ToList();
            Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, events.Count);
            events.ForEach(e => {
                Assert.False(e.IsHidden);
                var ri = e.GetRequestInfo();
                Assert.NotNull(ri);
                Assert.Equal(_clientIpAddress, ri.ClientIpAddress);
            });

            _repository.HideAllByClientIpAndDate(TestConstants.OrganizationId, _clientIpAddress, DateTime.UtcNow.SubtractDays(3), DateTime.UtcNow.AddDays(2));

            _client.Refresh();
            events = _repository.GetByStackId(TestConstants.StackId2, new PagingOptions().WithLimit(NUMBER_OF_EVENTS_TO_CREATE)).ToList();
            Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, events.Count);
            events.ForEach(e => Assert.True(e.IsHidden));
        }

        private readonly List<Tuple<string, DateTime>> _ids = new List<Tuple<string, DateTime>>();

        protected void CreateData() {
            var baseDate = DateTime.Now;
            var occurrenceDateStart = baseDate.AddMinutes(-30);
            var occurrenceDateMid = baseDate;
            var occurrenceDateEnd = baseDate.AddMinutes(30);

            _stackRepository.Add(StackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId));

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
                var ev = _repository.Add(EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: date));
                _ids.Add(Tuple.Create(ev.Id, date));
            }
        }

        protected void RemoveData() {
            _repository.RemoveAll();
            _stackRepository.RemoveAll();
        }
    }
}