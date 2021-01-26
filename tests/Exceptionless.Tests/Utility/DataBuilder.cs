using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Extensions;
using Foundatio.Repositories.Utility;
using Foundatio.Serializer;
using Foundatio.Utility;

namespace Exceptionless.Tests.Utility {
    public class DataBuilder {
        private readonly List<EventDataBuilder> _eventBuilders;
        private readonly IServiceProvider _serviceProvider;

        public DataBuilder(List<EventDataBuilder> eventBuilders, IServiceProvider serviceProvider) {
            _eventBuilders = eventBuilders;
            _serviceProvider = serviceProvider;
        }

        public EventDataBuilder Event() {
            var eventBuilder = _serviceProvider.GetService<EventDataBuilder>();
            _eventBuilders.Add(eventBuilder);
            return eventBuilder;
        }
    }

    public class EventDataBuilder {
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly ISerializer _serializer;
        private readonly ICollection<Action<Stack>> _stackMutations;
        private int _additionalEventsToCreate = 0;
        private readonly PersistentEvent _event = new PersistentEvent();
        private Stack _stack = null;
        private EventDataBuilder _stackEventBuilder;
        private bool _isFirstOccurrenceSet = false;

        public EventDataBuilder(FormattingPluginManager formattingPluginManager, ISerializer serializer) {
            _stackMutations = new List<Action<Stack>>();
            _formattingPluginManager = formattingPluginManager;
            _serializer = serializer;
        }

        public EventDataBuilder Mutate(Action<PersistentEvent> mutation) {
            mutation?.Invoke(_event);

            return this;
        }

        public EventDataBuilder MutateStack(Action<Stack> mutation) {
            _stackMutations.Add(mutation);

            return this;
        }

        public EventDataBuilder Stack(EventDataBuilder stackEventBuilder) {
            _stackEventBuilder = stackEventBuilder;

            return this;
        }

        public EventDataBuilder Stack(Stack stack) {
            _stack = stack;

            return this;
        }

        public EventDataBuilder StackId(string stackId) {
            _event.StackId = stackId;
            _stackMutations.Add(s => s.Id = stackId);

            return this;
        }

        public EventDataBuilder Id(string id) {
            _event.Id = id;
            return this;
        }

        public EventDataBuilder TestProject() {
            Organization(SampleDataService.TEST_ORG_ID);
            Project(SampleDataService.TEST_PROJECT_ID);

            return this;
        }

        public EventDataBuilder FreeProject() {
            Organization(SampleDataService.FREE_ORG_ID);
            Project(SampleDataService.FREE_PROJECT_ID);

            return this;
        }

        public EventDataBuilder Organization(string organizationId) {
            _event.OrganizationId = organizationId;
            return this;
        }

        public EventDataBuilder Project(string projectId) {
            _event.ProjectId = projectId;
            return this;
        }

        public EventDataBuilder Type(string type) {
            _event.Type = type;
            return this;
        }

        public EventDataBuilder Date(DateTimeOffset date) {
            _event.Date = date;
            return this;
        }

        public EventDataBuilder Date(DateTime date) {
            _event.Date = date.ToUniversalTime();
            return this;
        }

        public EventDataBuilder Date(string date) {
            if (DateTimeOffset.TryParse(date, out var dt))
                return Date(dt);
            
            throw new ArgumentException("Invalid date specified", nameof(date));
        }

        public EventDataBuilder IsFirstOccurrence(bool isFirstOccurrence = true) {
            _isFirstOccurrenceSet = true;
            _event.IsFirstOccurrence = isFirstOccurrence;

            return this;
        }

        public EventDataBuilder CreatedDate(DateTime createdUtc) {
            _event.CreatedUtc = createdUtc;
            return this;
        }

        public EventDataBuilder CreatedDate(string createdUtc) {
            if (DateTime.TryParse(createdUtc, out var dt))
                return CreatedDate(dt);
            
            throw new ArgumentException("Invalid date specified", nameof(createdUtc));
        }

        public EventDataBuilder Message(string message) {
            _event.Message = message;
            _stackMutations.Add(s => s.Title = message);
            return this;
        }

        public EventDataBuilder Source(string source) {
            _event.Source = source;
            return this;
        }

        public EventDataBuilder Tag(params string[] tags) {
            _event.Tags.AddRange(tags);
            return this;
        }

        public EventDataBuilder Geo(string geo) {
            _event.Geo = geo;
            return this;
        }

        public EventDataBuilder Value(decimal? value) {
            _event.Value = value;
            return this;
        }

        public EventDataBuilder EnvironmentInfo(EnvironmentInfo environmentInfo) {
            _event.SetEnvironmentInfo(environmentInfo);
            return this;
        }

        public EventDataBuilder RequestInfo(RequestInfo requestInfo) {
            _event.AddRequestInfo(requestInfo);
            return this;
        }

        public EventDataBuilder RequestInfo(string json) {
            _event.AddRequestInfo(_serializer.Deserialize<RequestInfo>(json));
            return this;
        }

        public EventDataBuilder RequestInfoSample(Action<RequestInfo> requestMutator = null) {
            var requestInfo = _serializer.Deserialize<RequestInfo>(_sampleRequestInfo);
            requestMutator?.Invoke(requestInfo);
            _event.AddRequestInfo(requestInfo);

            return this;
        }

        public EventDataBuilder ReferenceId(string id) {
            _event.ReferenceId = id;
            return this;
        }

        public EventDataBuilder Reference(string name, string id) {
            _event.SetEventReference(name, id);
            return this;
        }

        public EventDataBuilder UserDescription(string emailAddress, string description) {
            _event.SetUserDescription(emailAddress, description);
            return this;
        }

        public EventDataBuilder ManualStackingKey(string title, string manualStackingKey) {
            _event.SetManualStackingKey(title, manualStackingKey);
            return this;
        }

        public EventDataBuilder ManualStackingKey(string manualStackingKey) {
            _event.SetManualStackingKey(manualStackingKey);
            return this;
        }

        public EventDataBuilder SessionId(string sessionId) {
            _event.SetSessionId(sessionId);
            return this;
        }

        public EventDataBuilder SubmissionClient(SubmissionClient submissionClient) {
            _event.SetSubmissionClient(submissionClient);
            return this;
        }

        public EventDataBuilder UserIdentity(string identity) {
            _event.SetUserIdentity(identity);
            return this;
        }

        public EventDataBuilder UserIdentity(string identity, string name) {
            _event.SetUserIdentity(identity, name);
            return this;
        }

        public EventDataBuilder UserIdentity(UserInfo userInfo) {
            _event.SetUserIdentity(userInfo);
            return this;
        }

        public EventDataBuilder Level(string level) {
            _event.SetLevel(level);
            return this;
        }

        public EventDataBuilder Version(string version) {
            _event.SetVersion(version);
            return this;
        }

        public EventDataBuilder Location(Location location) {
            _event.SetLocation(location);
            return this;
        }

        public EventDataBuilder Deleted() {
            _stackMutations.Add(s => s.IsDeleted = true);

            return this;
        }

        public EventDataBuilder Status(StackStatus status) {
            _stackMutations.Add(s => s.Status = status);

            return this;
        }

        public EventDataBuilder StackReference(string reference) {
            _stackMutations.Add(s => s.References.Add(reference));

            return this;
        }

        public EventDataBuilder OccurrencesAreCritical(bool occurrencesAreCritical = true) {
            if (occurrencesAreCritical)
                _event.MarkAsCritical();

            _stackMutations.Add(s => {
                s.OccurrencesAreCritical = occurrencesAreCritical;
                s.Tags.Add(Event.KnownTags.Critical);
            });
            return this;
        }

        public EventDataBuilder TotalOccurrences(int totalOccurrences) {
            _stackMutations.Add(s => s.TotalOccurrences = totalOccurrences);

            return this;
        }
        
        public EventDataBuilder Create(int additionalOccurrences) {
            _additionalEventsToCreate = additionalOccurrences;
            _stackMutations.Add(s => {
                if (s.TotalOccurrences <= additionalOccurrences)
                    s.TotalOccurrences = additionalOccurrences + 1;
            });

            return this;
        }
        
        public EventDataBuilder FirstOccurrence(DateTime firstOccurrenceUtc) {
            _event.CreatedUtc = firstOccurrenceUtc;
            _stackMutations.Add(s => s.FirstOccurrence = firstOccurrenceUtc);

            return this;
        }

        public EventDataBuilder FirstOccurrence(string firstOccurrenceUtc) {
            if (DateTime.TryParse(firstOccurrenceUtc, out var dt))
                return FirstOccurrence(dt);
            
            throw new ArgumentException("Invalid date specified", nameof(firstOccurrenceUtc));
        }

        public EventDataBuilder LastOccurrence(DateTime lastOccurrenceUtc) {
            if (_event.CreatedUtc.IsAfter(lastOccurrenceUtc))
                _event.CreatedUtc = lastOccurrenceUtc;

            if (_event.Date.IsAfter(lastOccurrenceUtc))
                _event.Date = lastOccurrenceUtc;
            
            _stackMutations.Add(s => {
                if (s.FirstOccurrence.IsAfter(lastOccurrenceUtc))
                    s.FirstOccurrence = lastOccurrenceUtc;
                
                s.LastOccurrence = lastOccurrenceUtc;
            });
            
            return this;
        }

        public EventDataBuilder LastOccurrence(string lastOccurrenceUtc) {
            if (DateTime.TryParse(lastOccurrenceUtc, out var dt))
                return LastOccurrence(dt);
            
            throw new ArgumentException("Invalid date specified", nameof(lastOccurrenceUtc));
        }

        public EventDataBuilder DateFixed(DateTime? dateFixed = null) {
            Status(StackStatus.Fixed);
            _stackMutations.Add(s => {
                var fixedOn = dateFixed ?? SystemClock.UtcNow;
                if (s.FirstOccurrence.IsAfter(fixedOn))
                    throw new ArgumentException("Fixed on date is before first occurence");
                
                s.DateFixed = fixedOn;
            });

            return this;
        }

        public EventDataBuilder DateFixed(string dateFixedUtc) {
            if (DateTime.TryParse(dateFixedUtc, out var dt))
                return DateFixed(dt);
            
            throw new ArgumentException("Invalid date specified", nameof(dateFixedUtc));
        }

        public EventDataBuilder FixedInVersion(string version) {
            Status(StackStatus.Fixed);
            _stackMutations.Add(s => s.FixedInVersion = version);

            return this;
        }

        public EventDataBuilder Snooze(DateTime? snoozeUntil = null) {
            Status(StackStatus.Snoozed);
            _stackMutations.Add(s => s.SnoozeUntilUtc = snoozeUntil ?? SystemClock.UtcNow.AddDays(1));

            return this;
        }

        public Stack GetStack() {
            Build();
            return _stack;
        }

        private bool _isBuilt = false;
        public (Stack Stack, PersistentEvent[] Events) Build() {
            if (_isBuilt) 
                return (_stack, BuildEvents(_stack, _event));

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
            } else if (_stack == null) {
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
            return (_stack, BuildEvents(_stack, _event));
        }

        private PersistentEvent[] BuildEvents(Stack stack, PersistentEvent ev) {
            var events = new List<PersistentEvent>(_additionalEventsToCreate) { ev };
            if (_additionalEventsToCreate <= 0) 
                return events.ToArray();
            
            int interval = (stack.LastOccurrence - stack.FirstOccurrence).Milliseconds / _additionalEventsToCreate;
            for (int index = 0; index < stack.TotalOccurrences - 1; index++) {
                var clone = ev.DeepClone();
                clone.Id = null;
                if (interval > 0)
                    clone.Date = new DateTimeOffset(stack.FirstOccurrence.AddMilliseconds(interval * index), ev.Date.Offset);

                events.Add(clone);
            }

            return events.ToArray();
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
