using System;
using System.Collections.Generic;
using Exceptionless.Core.Repositories.Queries;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Search {
    public class EventStackFilterQueryVisitorTests : TestWithServices {
        public EventStackFilterQueryVisitorTests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [MemberData(nameof(FilterData.TestCases), MemberType = typeof(FilterData))]
        public void CanBuildStackFilter(FilterScenario scenario) {
            Log.SetLogLevel<EventStackFilterQueryVisitor>(Microsoft.Extensions.Logging.LogLevel.Trace);

            var stackResult = EventStackFilterQueryVisitor.Run(scenario.Source, EventStackFilterQueryMode.Stacks);
            Assert.Equal(scenario.Stack, stackResult.Query.Trim());
        }

        [Theory]
        [MemberData(nameof(FilterData.TestCases), MemberType = typeof(FilterData))]
        public void CanBuildInvertedStackFilter(FilterScenario scenario) {
            Log.SetLogLevel<EventStackFilterQueryVisitor>(Microsoft.Extensions.Logging.LogLevel.Trace);

            var invertedStackResult = EventStackFilterQueryVisitor.Run(scenario.Source, EventStackFilterQueryMode.InvertedStacks);
            Assert.Equal(scenario.InvertedStack, invertedStackResult.Query.Trim());
        }

        [Theory]
        [MemberData(nameof(FilterData.TestCases), MemberType = typeof(FilterData))]
        public void CanBuildEventFilter(FilterScenario scenario) {
            Log.SetLogLevel<EventStackFilterQueryVisitor>(Microsoft.Extensions.Logging.LogLevel.Trace);

            var eventResult = EventStackFilterQueryVisitor.Run(scenario.Source, EventStackFilterQueryMode.Events);
            Assert.Equal(scenario.Event, eventResult.Query.Trim());
        }
    }

    public class FilterData {
        public static IEnumerable<object[]> TestCases() {
            yield return new object[] { new FilterScenario {
                Source = "blah",
                Stack = "",
                InvertedStack = "",
                Event = "blah"
            }};
            yield return new object[] { new FilterScenario {
                Source = "status:fixed",
                Stack = "status:fixed",
                InvertedStack = "NOT status:fixed",
                Event = ""
            }};
            yield return new object[] { new FilterScenario {
                Source = "is_fixed:true",
                Stack = "status:fixed",
                InvertedStack = "NOT status:fixed",
                Event = ""
            }};
            yield return new object[] { new FilterScenario {
                Source = "is_regressed:true",
                Stack = "status:regressed",
                InvertedStack = "NOT status:regressed",
                Event = ""
            }};
            yield return new object[] { new FilterScenario {
                Source = "is_hidden:true",
                Stack = "NOT (status:open AND status:regressed)",
                InvertedStack = "(status:open AND status:regressed)",
                Event = ""
            }};
            yield return new object[] { new FilterScenario {
                Source = "is_hidden:false",
                Stack = "(status:open OR status:regressed)",
                InvertedStack = "NOT (status:open OR status:regressed)",
                Event = ""
            }};
            yield return new object[] { new FilterScenario {
                Source = "blah:true (status:fixed OR status:open)",
                Stack = "(status:fixed OR status:open)",
                InvertedStack = "NOT (status:fixed OR status:open)",
                Event = "blah:true"
            }};
            yield return new object[] { new FilterScenario {
                Source = "blah:true",
                Stack = "",
                InvertedStack = "",
                Event = "blah:true"
            }};
            yield return new object[] { new FilterScenario {
                Source = "type:session",
                Stack = "type:session",
                InvertedStack = "type:session",
                Event = "type:session"
            }};
            yield return new object[] { new FilterScenario {
                Source = "(organization:123 AND type:log) AND (blah:true (status:fixed OR status:open))",
                Stack = "(organization:123 AND type:log) AND (status:fixed OR status:open)",
                InvertedStack = "(organization:123 AND type:log) AND NOT (status:fixed OR status:open)",
                Event = "(organization:123 AND type:log) AND (blah:true)"
            }};
            yield return new object[] { new FilterScenario {
                Source = "project:123 (status:open OR status:regressed) (ref.session:5f3dce2668de920001466635)",
                Stack = "project:123 (status:open OR status:regressed)",
                InvertedStack = "project:123 NOT (status:open OR status:regressed)",
                Event = "project:123 (ref.session:5f3dce2668de920001466635)"
            }};
            yield return new object[] { new FilterScenario {
                Source = "project:123 (status:open OR status:regressed) (ref.session:5f3dce2668de920001466635 OR project:234)",
                Stack = "project:123 (status:open OR status:regressed) (project:234)",
                InvertedStack = "project:123 NOT (status:open OR status:regressed) (project:234)",
                Event = "project:123 (ref.session:5f3dce2668de920001466635 OR project:234)"
            }};
            yield return new object[] { new FilterScenario {
                Source = "first_occurrence:[1608854400000 TO 1609188757249] AND ((status:open OR status:regressed))",
                Stack = "first_occurrence:[1608854400000 TO 1609188757249] AND (status:open OR status:regressed)",
                InvertedStack = "NOT (first_occurrence:[1608854400000 TO 1609188757249] AND (status:open OR status:regressed))",
                Event = ""
            }};
        }
    }

    public class FilterScenario : IXunitSerializable {
        public string Source { get; set; } = String.Empty;
        public string Stack { get; set; } = String.Empty;
        public string InvertedStack { get; set; } = String.Empty;
        public string Event { get; set; } = String.Empty;

        public override string ToString() {
            return $"Source: \"{Source}\" Stack: \"{Stack}\" InvertedStack: \"{InvertedStack}\" Event: \"{Event}\"";
        }

        public void Deserialize(IXunitSerializationInfo info) {
            var value = JsonConvert.DeserializeObject<FilterScenario>(info.GetValue<string>("objValue"));
            Source = value.Source;
            Stack = value.Stack;
            InvertedStack = value.InvertedStack;
            Event = value.Event;
        }

        public void Serialize(IXunitSerializationInfo info) {
            var json = JsonConvert.SerializeObject(this);
            info.AddValue("objValue", json);
        }
    }
}
