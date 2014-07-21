using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using MongoDB.Bson;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class EventRepositoryTests : IDisposable {
        private readonly IEventRepository _repository = IoC.GetInstance<IEventRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();

        public EventRepositoryTests() {
            RemoveData();
            CreateData();
        }
        
        [Fact]
        public void GetPreviousEventOccurrenceIdTest() {
            Debug.WriteLine("Actual order:");
            foreach (var t in _ids)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.LocalDateTime.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Sorted order:");
            List<Tuple<string, DateTimeOffset>> sortedIds = _ids.OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1).ToList();
            foreach (var t in sortedIds)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.LocalDateTime.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Tests:");
            Assert.Equal(_ids.Count, _repository.Count());
            for (int i = 0; i < sortedIds.Count; i++) {
                Debug.WriteLine("Current - {0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.LocalDateTime.ToLongTimeString());
                if (i == 0)
                    Assert.Null(_repository.GetPreviousEventIdInStack(sortedIds[i].Item1));
                else
                    Assert.Equal(sortedIds[i - 1].Item1, _repository.GetPreviousEventIdInStack(sortedIds[i].Item1));
            }
        }

        [Fact]
        public void GetNextEventOccurrenceIdTest() {
            Debug.WriteLine("Actual order:");
            foreach (var t in _ids)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.LocalDateTime.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Sorted order:");
            List<Tuple<string, DateTimeOffset>> sortedIds = _ids.OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1).ToList();
            foreach (var t in sortedIds)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.LocalDateTime.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Tests:");
            Assert.Equal(_ids.Count, _repository.Count());
            for (int i = 0; i < sortedIds.Count; i++) {
                Debug.WriteLine("Current - {0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.LocalDateTime.ToLongTimeString());
                string nextId = _repository.GetNextEventIdInStack(sortedIds[i].Item1);
                if (i == sortedIds.Count - 1)
                    Assert.Null(nextId);
                else
                    Assert.Equal(sortedIds[i + 1].Item1, nextId);
            }
        }

        [Fact]
        public void GetByOrganizationIdsPaged() {
            Debug.WriteLine("Sorted order:");
            List<Tuple<string, DateTimeOffset>> sortedIds = _ids.OrderByDescending(t => t.Item2.Ticks).ThenByDescending(t => t.Item1).ToList();
            foreach (var t in sortedIds)
                Debug.WriteLine("{0}: {1}", t.Item1, t.Item2.LocalDateTime.ToLongTimeString());

            Debug.WriteLine("");
            Debug.WriteLine("Before {0}: {1}", sortedIds[2].Item1, sortedIds[2].Item2.LocalDateTime.ToLongTimeString());
            var results = _repository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithBefore(String.Concat(sortedIds[2].Item2.UtcTicks.ToString(), "-", sortedIds[2].Item1))).ToArray();
            Assert.True(results.Length > 0);

            for (int i = 0; i < sortedIds.Count - 3; i++) {
                Debug.WriteLine("{0}: {1}", sortedIds[i + 3].Item1, sortedIds[i + 3].Item2.LocalDateTime.ToLongTimeString());
                Assert.Equal(sortedIds[i + 3].Item1, results[i].Id);
            }

            Debug.WriteLine("");
            Debug.WriteLine("After {0}: {1}", sortedIds[2].Item1, sortedIds[2].Item2.LocalDateTime.ToLongTimeString());
            results = _repository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithAfter(String.Concat(sortedIds[2].Item2.UtcTicks.ToString(), "-", sortedIds[2].Item1))).ToArray();
            Assert.True(results.Length > 0);

            for (int i = 0; i < results.Length; i++) {
                Debug.WriteLine("{0}: {1}", sortedIds[i].Item1, sortedIds[i].Item2.LocalDateTime.ToLongTimeString());
                Assert.Equal(sortedIds[i].Item1, results[i].Id);
            }

            Debug.WriteLine("");
            Debug.WriteLine("Between {0}: {1} and {2}: {3}", sortedIds[4].Item1, sortedIds[4].Item2.LocalDateTime.ToLongTimeString(), sortedIds[1].Item1, sortedIds[1].Item2.LocalDateTime.ToLongTimeString());
            results = _repository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithAfter(String.Concat(sortedIds[4].Item2.UtcTicks.ToString(), "-", sortedIds[4].Item1)).WithBefore(String.Concat(sortedIds[1].Item2.UtcTicks.ToString(), "-", sortedIds[1].Item1))).ToArray();
            Assert.True(results.Length > 0);

            for (int i = 0; i < results.Length; i++) {
                Debug.WriteLine("{0}: {1}", sortedIds[i + 2].Item1, sortedIds[i + 2].Item2.LocalDateTime.ToLongTimeString());
                Assert.Equal(sortedIds[i + 2].Item1, results[i].Id);
            }
        }

        private readonly List<Tuple<string, DateTimeOffset>> _ids = new List<Tuple<string, DateTimeOffset>>();

        protected void CreateData() {
            var baseDate = DateTimeOffset.Now;
            var occurrenceDateStart = baseDate.AddMinutes(-30);
            var occurrenceDateMid = baseDate;
            var occurrenceDateEnd = baseDate.AddMinutes(30);

            _stackRepository.Add(StackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), occurrenceDateStart));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), occurrenceDateEnd));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), baseDate.AddMinutes(-10)));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), baseDate.AddMinutes(-20)));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), occurrenceDateMid));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), occurrenceDateMid));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), occurrenceDateMid));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), baseDate.AddMinutes(20)));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), baseDate.AddMinutes(10)));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), baseDate.AddSeconds(1)));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), occurrenceDateEnd));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));

            _ids.Add(Tuple.Create(ObjectId.GenerateNewId().ToString(), occurrenceDateStart));
            _repository.Add(EventData.GenerateEvent(id: _ids.Last().Item1, projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: _ids.Last().Item2, nestingLevel: 5, minimiumNestingLevel: 1));
        }

        protected void RemoveData() {
            _repository.RemoveAll(false);
            _stackRepository.RemoveAll(false);
        }

        public void Dispose() {
            RemoveData();
        }
    }
}