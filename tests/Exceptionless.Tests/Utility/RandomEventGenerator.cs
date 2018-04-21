using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Utility;

namespace Exceptionless.Helpers {
    public class RandomEventGenerator {
        public DateTime? MinDate { get; set; }
        public DateTime? MaxDate { get; set; }

        public List<Event> Generate(int count, bool setUserIdentity = true) {
            var events = new List<Event>();
            for (int i = 0; i < count; i++)
                events.Add(Generate(setUserIdentity));

            return events;
        }

        public PersistentEvent GeneratePersistent(bool setUserIdentity = true) {
            var ev = new PersistentEvent {
                OrganizationId = "1ecd0826e447ad1e78877555",
                ProjectId = "1ecd0826e447ad1e78877ab2",
                StackId = "1ecd0826e447a44e78877ab1",
                Date = SystemClock.UtcNow
            };

            PopulateEvent(ev, setUserIdentity);
            return ev;
        }

        public Event Generate(bool setUserIdentity = true) {
            var ev = new Event();
            PopulateEvent(ev, setUserIdentity);
            return ev;
        }

        public void PopulateEvent(Event ev, bool setUserIdentity = true) {
            if (MinDate.HasValue || MaxDate.HasValue)
                ev.Date = RandomData.GetDateTime(MinDate ?? DateTime.MinValue, MaxDate ?? DateTime.MaxValue);

            ev.Type = new [] { Event.KnownTypes.Error, Event.KnownTypes.FeatureUsage, Event.KnownTypes.Log, Event.KnownTypes.NotFound }.Random();
            if (ev.Type == Event.KnownTypes.FeatureUsage)
                ev.Source = FeatureNames.Random();
            else if (ev.Type == Event.KnownTypes.NotFound)
                ev.Source = PageNames.Random();
            else if (ev.Type == Event.KnownTypes.Log) {
                ev.Source = LogSources.Random();
                ev.Message = RandomData.GetString();

                string level = LogLevels.Random();
                if (!String.IsNullOrEmpty(level))
                    ev.Data[Event.KnownDataKeys.Level] = level;
            }

            if (RandomData.GetBool(80))
                ev.Geo = RandomData.GetCoordinate();

            if (RandomData.GetBool(20))
                ev.Value = RandomData.GetInt(0, 10000);

            if (setUserIdentity)
                ev.SetUserIdentity(Identities.Random());

            ev.SetVersion(RandomData.GetVersion("2.0", "4.0"));

            ev.AddRequestInfo(new RequestInfo {
                //ClientIpAddress = ClientIpAddresses.Random(),
                Path = PageNames.Random()
            });

            ev.Data.Add(Event.KnownDataKeys.EnvironmentInfo, new EnvironmentInfo {
                IpAddress = MachineIpAddresses.Random() + ", " + MachineIpAddresses.Random(),
                MachineName = MachineNames.Random()
            });

            for (int i = 0; i < RandomData.GetInt(1, 3); i++) {
                string key = RandomData.GetWord();
                while (ev.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                    key = RandomData.GetWord();

                ev.Data.Add(key, RandomData.GetString());
            }

            int tagCount = RandomData.GetInt(1, 3);
            for (int i = 0; i < tagCount; i++) {
                string tag = EventTags.Random();
                if (!ev.Tags.Contains(tag))
                    ev.Tags.Add(tag);
            }

            if (ev.Type == Event.KnownTypes.Error) {
                if (RandomData.GetBool()) {
                    // limit error variation so that stacking will occur
                    if (_randomErrors == null)
                        _randomErrors = new List<Error>(Enumerable.Range(1, 25).Select(i => GenerateError()));

                    ev.Data[Event.KnownDataKeys.Error] = _randomErrors.Random();
                } else {
                    // limit error variation so that stacking will occur
                    if (_randomSimpleErrors == null)
                        _randomSimpleErrors = new List<SimpleError>(Enumerable.Range(1, 25).Select(i => GenerateSimpleError()));

                    ev.Data[Event.KnownDataKeys.SimpleError] = _randomSimpleErrors.Random();
                }
            }
        }

        private List<Error> _randomErrors;

        public Error GenerateError(int maxErrorNestingLevel = 3, bool generateData = true, int currentNestingLevel = 0) {
            var error = new Error { Message = @"Generated exception message.", Type = ExceptionTypes.Random() };
            if (RandomData.GetBool())
                error.Code = RandomData.GetInt(-234523453, 98690899).ToString();

            if (generateData) {
                for (int i = 0; i < RandomData.GetInt(1, 5); i++) {
                    string key = RandomData.GetWord();
                    while (error.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                        key = RandomData.GetWord();

                    error.Data.Add(key, RandomData.GetString());
                }
            }

            var stack = new StackFrameCollection();
            for (int i = 0; i < RandomData.GetInt(1, 10); i++)
                stack.Add(GenerateStackFrame());
            error.StackTrace = stack;

            if (currentNestingLevel < maxErrorNestingLevel && RandomData.GetBool())
                error.Inner = GenerateError(maxErrorNestingLevel, generateData, currentNestingLevel + 1);

            return error;
        }

        private List<SimpleError> _randomSimpleErrors;

        public SimpleError GenerateSimpleError(int maxErrorNestingLevel = 3, bool generateData = true, int currentNestingLevel = 0) {
            var error = new SimpleError { Message = @"Generated exception message.", Type = ExceptionTypes.Random() };
            if (generateData) {
                for (int i = 0; i < RandomData.GetInt(1, 5); i++) {
                    string key = RandomData.GetWord();
                    while (error.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                        key = RandomData.GetWord();

                    error.Data.Add(key, RandomData.GetString());
                }
            }

            error.StackTrace = RandomData.GetString();

            if (currentNestingLevel < maxErrorNestingLevel && RandomData.GetBool())
                error.Inner = GenerateSimpleError(maxErrorNestingLevel, generateData, currentNestingLevel + 1);

            return error;
        }

        public StackFrame GenerateStackFrame() {
            return new StackFrame {
                DeclaringNamespace = Namespaces.Random(),
                DeclaringType = TypeNames.Random(),
                Name = MethodNames.Random(),
                Parameters = new ParameterCollection {
                    new Parameter {
                        Type = "String",
                        Name = "path"
                    }
                }
            };
        }

        #region Sample Data

        public readonly List<string> Identities = new List<string> {
            "eric@exceptionless.io",
            "blake@exceptionless.io",
            "marylou@exceptionless.io"
        };

        public readonly List<string> MachineIpAddresses = new List<string> {
            "127.34.36.89",
            "45.66.89.98",
            "10.12.18.193",
            "16.89.17.197",
            "43.10.99.234"
        };

        public readonly List<string> ClientIpAddresses = new List<string> {
            "77.23.23.78",
            "45.66.89.98",
            "10.12.18.193",
            "89.23.45.98",
            "231.23.34.1"
        };

        public readonly List<string> LogSources = new List<string> {
            "Some.Class",
            "MyClass",
            "CodeGenerator",
            "Exceptionless.Core.Parser.SomeClass"
        };

        public readonly List<string> LogLevels = new List<string> {
            "Trace",
            "Info",
            "Debug",
            "Warn",
            "Error",
            "Custom"
        };

        public readonly List<string> FeatureNames = new List<string> {
            "Feature1",
            "Feature2",
            "Feature3",
            "Feature4"
        };

        public readonly List<string> MachineNames = new List<string> {
            "machine1",
            "machine2",
            "machine3",
            "machine4"
        };

        public readonly List<string> PageNames = new List<string> {
            "/page1",
            "/page2",
            "/page3",
            "/page4"
        };

        public readonly List<string> EventTypes = new List<string> {
            Event.KnownTypes.Error,
            Event.KnownTypes.FeatureUsage,
            Event.KnownTypes.Log,
            Event.KnownTypes.NotFound,
            Event.KnownTypes.Session,
            Event.KnownTypes.SessionEnd
        };

        public readonly List<string> ExceptionTypes = new List<string> {
            "System.NullReferenceException",
            "System.ApplicationException",
            "System.AggregateException",
            "System.InvalidArgumentException",
            "System.InvalidOperationException"
        };

        public readonly List<string> EventTags = new List<string> {
            "Tag1",
            "Tag2",
            "Tag3",
            "Tag4",
            "Tag5",
            "Tag6",
            "Tag7",
            "Tag8",
            "Tag9",
            "Tag10"
        };

        public readonly List<string> Namespaces = new List<string> {
            "System",
            "System.IO",
            "CodeSmith",
            "CodeSmith.Generator",
            "SomeOther.Blah"
        };

        public readonly List<string> TypeNames = new List<string> {
            "DateTime",
            "SomeType",
            "ProjectGenerator"
        };

        public readonly List<string> MethodNames = new List<string> {
            "SomeMethod",
            "GenerateCode"
        };

        #endregion
    }
}
