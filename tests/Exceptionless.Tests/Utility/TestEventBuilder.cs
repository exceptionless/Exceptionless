using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Utility;
using Exceptionless.Extensions;
using Foundatio.Repositories.Utility;
using Foundatio.Serializer;
using Foundatio.Utility;

namespace Exceptionless.Tests.Utility {
    public class TestEventBuilder {
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly ISerializer _serializer;
        private readonly ICollection<Action<Stack>> _stackMutations;
        private PersistentEvent _event = new PersistentEvent();
        private Stack _stack = null;
        private TestEventBuilder _stackEventBuilder;
        private bool _isFirstOccurrenceSet = false;

        public TestEventBuilder(FormattingPluginManager formattingPluginManager, ISerializer serializer) {
            _stackMutations = new List<Action<Stack>>();
            _formattingPluginManager = formattingPluginManager;
            _serializer = serializer;
        }

        public TestEventBuilder Mutate(Action<PersistentEvent> mutation) {
            mutation?.Invoke(_event);

            return this;
        }

        public TestEventBuilder MutateStack(Action<Stack> mutation) {
            _stackMutations.Add(mutation);

            return this;
        }

        public TestEventBuilder Stack(TestEventBuilder stackEventBuilder) {
            _stackEventBuilder = stackEventBuilder;

            return this;
        }

        public TestEventBuilder Stack(Stack stack) {
            _stack = stack;

            return this;
        }

        public TestEventBuilder StackId(string stackId) {
            _event.StackId = stackId;

            return this;
        }

        public TestEventBuilder Id(string id) {
            _event.Id = id;

            return this;
        }

        public TestEventBuilder TestProject() {
            Organization(SampleDataService.TEST_ORG_ID);
            Project(SampleDataService.TEST_PROJECT_ID);

            return this;
        }

        public TestEventBuilder FreeProject() {
            Organization(SampleDataService.FREE_ORG_ID);
            Project(SampleDataService.FREE_PROJECT_ID);

            return this;
        }

        public TestEventBuilder Organization(string organizationId) {
            _event.OrganizationId = organizationId;
            return this;
        }

        public TestEventBuilder Project(string projectId) {
            _event.ProjectId = projectId;
            return this;
        }

        public TestEventBuilder Type(string type) {
            _event.Type = type;
            return this;
        }

        public TestEventBuilder Date(DateTimeOffset date) {
            _event.Date = date;
            return this;
        }

        public TestEventBuilder Date(DateTime date) {
            _event.Date = date.ToUniversalTime();
            return this;
        }

        public TestEventBuilder Date(string date) {
            if (DateTimeOffset.TryParse(date, out var dt))
                _event.Date = dt;
            else
                throw new ArgumentException("Invalid date specified", nameof(date));

            return this;
        }

        public TestEventBuilder IsFirstOccurrence(bool isFirstOccurrence = true) {
            _isFirstOccurrenceSet = true;
            _event.IsFirstOccurrence = isFirstOccurrence;

            return this;
        }

        public TestEventBuilder CreatedDate(DateTime createdUtc) {
            _event.CreatedUtc = createdUtc;
            return this;
        }

        public TestEventBuilder CreatedDate(string createdUtc) {
            if (DateTime.TryParse(createdUtc, out var dt))
                _event.CreatedUtc = dt;
            else
                throw new ArgumentException("Invalid date specified", nameof(createdUtc));

            return this;
        }

        public TestEventBuilder Message(string message) {
            _event.Message = message;
            return this;
        }

        public TestEventBuilder Source(string source) {
            _event.Source = source;
            return this;
        }

        public TestEventBuilder Tag(params string[] tags) {
            _event.Tags.AddRange(tags);
            return this;
        }

        public TestEventBuilder Geo(string geo) {
            _event.Geo = geo;
            return this;
        }

        public TestEventBuilder Value(decimal? value) {
            _event.Value = value;
            return this;
        }

        public TestEventBuilder EnvironmentInfo(EnvironmentInfo environmentInfo) {
            _event.SetEnvironmentInfo(environmentInfo);
            return this;
        }

        public TestEventBuilder RequestInfo(RequestInfo requestInfo) {
            _event.AddRequestInfo(requestInfo);
            return this;
        }

        public TestEventBuilder RequestInfo(string json) {
            _event.AddRequestInfo(_serializer.Deserialize<RequestInfo>(json));
            return this;
        }

        public TestEventBuilder RequestInfoSample(Action<RequestInfo> requestMutator = null) {
            var requestInfo = _serializer.Deserialize<RequestInfo>(_sampleRequestInfo);
            requestMutator?.Invoke(requestInfo);
            _event.AddRequestInfo(requestInfo);

            return this;
        }

        public TestEventBuilder ReferenceId(string id) {
            _event.ReferenceId = id;
            return this;
        }

        public TestEventBuilder Reference(string name, string id) {
            _event.SetEventReference(name, id);
            return this;
        }

        public TestEventBuilder UserDescription(string emailAddress, string description) {
            _event.SetUserDescription(emailAddress, description);
            return this;
        }

        public TestEventBuilder ManualStackingKey(string title, string manualStackingKey) {
            _event.SetManualStackingKey(title, manualStackingKey);
            return this;
        }

        public TestEventBuilder ManualStackingKey(string manualStackingKey) {
            _event.SetManualStackingKey(manualStackingKey);
            return this;
        }

        public TestEventBuilder SessionId(string sessionId) {
            _event.SetSessionId(sessionId);
            return this;
        }

        public TestEventBuilder SubmissionClient(SubmissionClient submissionClient) {
            _event.SetSubmissionClient(submissionClient);
            return this;
        }

        public TestEventBuilder UserIdentity(string identity) {
            _event.SetUserIdentity(identity);
            return this;
        }

        public TestEventBuilder UserIdentity(string identity, string name) {
            _event.SetUserIdentity(identity, name);
            return this;
        }

        public TestEventBuilder UserIdentity(UserInfo userInfo) {
            _event.SetUserIdentity(userInfo);
            return this;
        }

        public TestEventBuilder Level(string level) {
            _event.SetLevel(level);
            return this;
        }

        public TestEventBuilder Version(string version) {
            _event.SetVersion(version);
            return this;
        }

        public TestEventBuilder Location(Location location) {
            _event.SetLocation(location);
            return this;
        }

        public TestEventBuilder Deleted() {
            _stackMutations.Add(s => s.IsDeleted = true);

            return this;
        }

        public TestEventBuilder Status(StackStatus status) {
            _stackMutations.Add(s => s.Status = StackStatus.Open);

            return this;
        }

        public TestEventBuilder StackReference(string reference) {
            _stackMutations.Add(s => s.References.Add(reference));

            return this;
        }

        public TestEventBuilder OccurrencesAreCritical(bool occurrencesAreCritical = true) {
            if (occurrencesAreCritical)
                _event.MarkAsCritical();

            _stackMutations.Add(s => s.OccurrencesAreCritical = occurrencesAreCritical);

            return this;
        }

        public TestEventBuilder TotalOccurrences(int totalOccurrences) {
            _stackMutations.Add(s => s.TotalOccurrences = totalOccurrences);

            return this;
        }

        public TestEventBuilder FirstOccurrence(DateTime firstOccurrenceUtc) {
            _stackMutations.Add(s => s.FirstOccurrence = firstOccurrenceUtc);

            return this;
        }

        public TestEventBuilder FirstOccurrence(string firstOccurrenceUtc) {
            if (DateTime.TryParse(firstOccurrenceUtc, out var dt))
                _event.CreatedUtc = dt;
            else
                throw new ArgumentException("Invalid date specified", nameof(firstOccurrenceUtc));

            _stackMutations.Add(s => s.FirstOccurrence = dt);

            return this;
        }

        public TestEventBuilder LastOccurrence(DateTime lastOccurrenceUtc) {
            _stackMutations.Add(s => s.LastOccurrence = lastOccurrenceUtc);

            return this;
        }

        public TestEventBuilder LastOccurrence(string lastOccurrenceUtc) {
            if (DateTime.TryParse(lastOccurrenceUtc, out var dt))
                _event.CreatedUtc = dt;
            else
                throw new ArgumentException("Invalid date specified", nameof(lastOccurrenceUtc));
            
            _stackMutations.Add(s => s.LastOccurrence = dt);

            return this;
        }

        public TestEventBuilder DateFixed(DateTime? dateFixed = null) {
            Status(StackStatus.Fixed);
            _stackMutations.Add(s => s.DateFixed = dateFixed ?? SystemClock.UtcNow);

            return this;
        }

        public TestEventBuilder DateFixed(string dateFixedUtc) {
            if (DateTime.TryParse(dateFixedUtc, out var dt))
                _event.CreatedUtc = dt;
            else
                throw new ArgumentException("Invalid date specified", nameof(dateFixedUtc));

            Status(StackStatus.Fixed);
            _stackMutations.Add(s => s.DateFixed = dt);

            return this;
        }

        public TestEventBuilder FixedInVersion(string version) {
            Status(StackStatus.Fixed);
            _stackMutations.Add(s => s.FixedInVersion = version);

            return this;
        }

        public TestEventBuilder Snooze(DateTime? snoozeUntil = null) {
            Status(StackStatus.Snoozed);
            _stackMutations.Add(s => s.SnoozeUntilUtc = snoozeUntil ?? SystemClock.UtcNow.AddDays(1));

            return this;
        }

        public Stack GetStack() {
            Build();
            return _stack;
        }

        private bool _isBuilt = false;
        public (Stack Stack, PersistentEvent Event) Build() {
            if (_isBuilt)
                return (_stack, _event);

            if (String.IsNullOrEmpty(_event.OrganizationId))
                _event.OrganizationId = SampleDataService.TEST_ORG_ID;
            if (String.IsNullOrEmpty(_event.ProjectId))
                _event.ProjectId = SampleDataService.TEST_PROJECT_ID;
            if (String.IsNullOrEmpty(_event.Type))
                _event.Type = Event.KnownTypes.Log;
            if (String.IsNullOrEmpty(_event.Source))
                _event.Source = "Test Event";
            if (_event.Date == DateTimeOffset.MinValue)
                _event.Date = SystemClock.OffsetNow;
            if (_event.CreatedUtc == DateTime.MinValue)
                _event.CreatedUtc = _event.Date.UtcDateTime;

            _event.CopyDataToIndex();

            if (_stackEventBuilder != null) {
                _stack = _stackEventBuilder.GetStack();

                _stack.TotalOccurrences++;
                if (_event.Date.UtcDateTime < _stack.FirstOccurrence) {
                    if (!_isFirstOccurrenceSet)
                        _event.IsFirstOccurrence = true;
                    _stack.FirstOccurrence = _event.Date.UtcDateTime;
                }

                if (_event.Date.UtcDateTime > _stack.LastOccurrence)
                    _stack.LastOccurrence = _event.Date.UtcDateTime;

                _stack.Tags.AddRange(_event.Tags ?? new TagSet());
            }
            else if (_stack == null) {
                string title = _formattingPluginManager.GetStackTitle(_event);
                _stack = new Stack {
                    OrganizationId = _event.OrganizationId,
                    ProjectId = _event.ProjectId,
                    Title = title?.Truncate(1000),
                    Tags = _event.Tags ?? new TagSet(),
                    Type = _event.Type,
                    TotalOccurrences = 1,
                    FirstOccurrence = _event.Date.UtcDateTime,
                    LastOccurrence = _event.Date.UtcDateTime
                };

                if (_event.Type == Event.KnownTypes.Session)
                    _stack.Status = StackStatus.Ignored;

                if (!_isFirstOccurrenceSet)
                    _event.IsFirstOccurrence = true;
            } else {
                _stack.TotalOccurrences++;
                if (_event.Date.UtcDateTime < _stack.FirstOccurrence) {
                    if (!_isFirstOccurrenceSet)
                        _event.IsFirstOccurrence = true;
                    _stack.FirstOccurrence = _event.Date.UtcDateTime;
                }

                if (_event.Date.UtcDateTime > _stack.LastOccurrence)
                    _stack.LastOccurrence = _event.Date.UtcDateTime;

                _stack.Tags.AddRange(_event.Tags ?? new TagSet());
            }

            foreach (var mutation in _stackMutations)
                mutation?.Invoke(_stack);

            if (_stack.FirstOccurrence < _stack.CreatedUtc)
                _stack.CreatedUtc = _stack.FirstOccurrence;

            if (_stack.FirstOccurrence < _event.Date)
                _event.IsFirstOccurrence = false;

            var msi = _event.GetManualStackingInfo();
            if (msi != null) {
                _stack.Title = msi.Title;
                _stack.SignatureInfo.Clear();
                _stack.SignatureInfo.AddRange(msi.SignatureData);
            }

            if (_stack.SignatureInfo.Count == 0) {
                _stack.SignatureInfo.AddItemIfNotEmpty("Type", _event.Type);
                _stack.SignatureInfo.AddItemIfNotEmpty("Source", _event.Source);
            }

            string signatureHash = _stack.SignatureInfo.Values.ToSHA1();
            _stack.SignatureHash = signatureHash;
            _stack.DuplicateSignature = _stack.ProjectId + ":" + signatureHash;

            if (String.IsNullOrEmpty(_stack.Id))
                _stack.Id = ObjectId.GenerateNewId().ToString();

            _event.StackId = _stack.Id;

            _isBuilt = true;

            return (_stack, _event);
        }

        private const string _sampleRequestInfo = @"{
          ""user_agent"": ""Mozilla/5.0 (Linux; Android 4.1.1; Prism II Build/HuaweiU8686) AppleWebKit/537.31 (KHTML, like Gecko) Chrome/26.0.1410.58 Mobile Safari/537.31"",
          ""http_method"": ""GET"",
          ""is_secure"": true,
          ""host"": ""app.exceptionless.com"",
          ""port"": 443,
          ""path"": ""/apple-touch-icon.png"",
          ""client_ip_address"": ""192.168.0.243,172.10.0.30"",
          ""data"": {
            ""@browser"": ""Chrome Mobile"",
            ""@browser_version"": ""26.0.1410"",
            ""@browser_major_version"": ""26"",
            ""@device"": ""Huawei U8686"",
            ""@os"": ""Android"",
            ""@os_version"": ""4.1.1"",
            ""@os_major_version"": ""4"",
            ""@is_bot"": true
          }
        }";
    }
}
