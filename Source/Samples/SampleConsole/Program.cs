#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Helpers;
using Exceptionless;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace SampleConsole {
    internal class Program {
        private static bool _sendingContinuous = false;

        private static int[] _delays = new[] { 0, 50, 100, 1000 };
        private static int _delayIndex = 2;

        private static TimeSpan[] _dateSpans = new TimeSpan[] {
            TimeSpan.Zero,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromHours(1),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(TimeSpanExtensions.AvgDaysInAMonth),
            TimeSpan.FromDays(TimeSpanExtensions.AvgDaysInAMonth * 3)
        };
        private static int _dateSpanIndex = 3;

        private static void Main() {
            ExceptionlessClient.Default.Startup();
            ExceptionlessClient.Default.Configuration.UseFolderStorage("store");
            ExceptionlessClient.Default.Configuration.UseFileLogger("store\\exceptionless.log");

            var tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            if (false) {
                ExceptionlessClient.Default.CreateLog("SampleConsole", "Has lots of extended data")
                    .AddObject(new { myApplicationVersion = new Version(1, 0), Date = DateTime.Now, __sessionId = "9C72E4E8-20A2-469B-AFB9-492B6E349B23", SomeField10 = "testing" }, "Object From Code")
                    .AddObject(new { Blah = "Test" }, name: "Test Object")
                    .AddObject("Exceptionless is awesome", "String Content")
                    .AddObject(new int[] { 1, 2, 3, 4, 5 }, "Array Content")
                    .AddObject(new object[] { new { This = "This" }, new { Is = "Is" }, new { A = "A" }, new { Test = "Test", Data = new { Punctuation = "!!!!" } } }, "Array With Nested Content")
                    .Submit();
                ExceptionlessClient.Default.SubmitFeatureUsage("MyFeature");
                ExceptionlessClient.Default.SubmitNotFound("/somepage");
                ExceptionlessClient.Default.SubmitSessionStart(Guid.NewGuid().ToString("N"));
            }
            ExceptionlessClient.Default.Configuration.AddEnrichment(ev => ev.Data[RandomHelper.GetPronouncableString(5)] = RandomHelper.GetPronouncableString(10));
            ExceptionlessClient.Default.Configuration.Settings.Changed += (sender, args) => Trace.WriteLine(String.Format("Action: {0} Key: {1} Value: {2}", args.Action, args.Item.Key, args.Item.Value ));

            WriteOptionsMenu();

            while (true) {
                Console.SetCursorPosition(0, _optionsMenuLineCount + 1);
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.D1)
                    SendEvent();
                else if (keyInfo.Key == ConsoleKey.D2)
                    SendContinuousEvents(50, token, 100);
                else if (keyInfo.Key == ConsoleKey.D3)
                    SendContinuousEvents(_delays[_delayIndex], token);
                else if (keyInfo.Key == ConsoleKey.D4) {
                    Console.SetCursorPosition(0, _optionsMenuLineCount + 2);
                    Console.WriteLine("Telling client to process the queue...");

                    ExceptionlessClient.Default.ProcessQueue();

                    ClearNonOptionsLines();
                } else if (keyInfo.Key == ConsoleKey.D5) {
                    SendAllCapturedEventsFromDisk();
                    ClearNonOptionsLines();
                } else if (keyInfo.Key == ConsoleKey.D) {
                    _dateSpanIndex++;
                    if (_dateSpanIndex == _dateSpans.Length)
                        _dateSpanIndex = 0;
                    WriteOptionsMenu();
                } else if (keyInfo.Key == ConsoleKey.T) {
                    _delayIndex++;
                    if (_delayIndex == _delays.Length)
                        _delayIndex = 0;
                    WriteOptionsMenu();
                } else if (keyInfo.Key == ConsoleKey.Q)
                    break;
                else if (keyInfo.Key == ConsoleKey.S) {
                    tokenSource.Cancel();
                    tokenSource = new CancellationTokenSource();
                    token = tokenSource.Token;
                    ClearNonOptionsLines();
                }
            }
        }

        private const int _optionsMenuLineCount = 9;
        private static void WriteOptionsMenu() {
            Console.SetCursorPosition(0, 0);
            ClearConsoleLines(0, _optionsMenuLineCount - 1);
            Console.WriteLine("1: Send 1");
            Console.WriteLine("2: Send 100");
            Console.WriteLine("3: Send continous");
            Console.WriteLine("4: Process queue");
            Console.WriteLine("5: Process directory");
            Console.WriteLine("D: Change date range (" + _dateSpans[_dateSpanIndex].ToWords() + ")");
            Console.WriteLine("T: Change continuous delay (" + _delays[_delayIndex].ToString("N0") + ")");
            Console.WriteLine();
            Console.WriteLine("Q: Quit");
        }

        private static void ClearNonOptionsLines(int delay = 1000) {
            Task.Factory.StartNewDelayed(delay, () => ClearConsoleLines(_optionsMenuLineCount));
        }

        private static void ClearConsoleLines(int startLine = 0, int endLine = -1) {
            if (endLine < 0)
                endLine = Console.WindowHeight - 2;

            int currentLine = Console.CursorTop;
            int currentPosition = Console.CursorLeft;

            for (int i = startLine; i <= endLine; i++) {
                Console.SetCursorPosition(0, i);
                Console.Write(new string(' ', Console.WindowWidth));
            }
            Console.SetCursorPosition(currentPosition, currentLine);
        }

        private static void SendContinuousEvents(int delay, CancellationToken token, int maxEvents = Int32.MaxValue, int maxDaysOld = 90) {
            _sendingContinuous = true;
            Console.SetCursorPosition(0, _optionsMenuLineCount + 2);
            Console.WriteLine("Press 's' to stop sending.");
            int eventCount = 0;

            Task.Factory.StartNew(delegate {
                while (eventCount < maxEvents) {
                    if (token.IsCancellationRequested) {
                        _sendingContinuous = false;
                        break;
                    }

                    SendEvent(false);
                    eventCount++;
                    Console.SetCursorPosition(0, _optionsMenuLineCount + 4);
                    Console.WriteLine("Sent {0} events.", eventCount);
                    Trace.WriteLine(String.Format("Sent {0} events.", eventCount));

                    Thread.Sleep(delay);
                }

                ClearNonOptionsLines();
            }, token);
        }

        private static void SendEvent(bool writeToConsole = true) {
            var ev = new Event();
            if (_dateSpans[_dateSpanIndex] != TimeSpan.Zero)
                ev.Date = RandomHelper.GetDateTime(DateTime.Now.Subtract(_dateSpans[_dateSpanIndex]), DateTime.Now);

            ev.Type = EventTypes.Random();
            if (ev.Type == Event.KnownTypes.FeatureUsage)
                ev.Source = FeatureNames.Random();
            else if (ev.Type == Event.KnownTypes.NotFound)
                ev.Source = PageNames.Random();
            else if (ev.Type == Event.KnownTypes.Log) {
                ev.Source = LogSources.Random();
                ev.Message = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
            }

            for (int i = 0; i < RandomHelper.GetRange(1, 5); i++) {
                string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 10));
                while (ev.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                    key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                ev.Data.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
            }

            for (int i = 0; i < RandomHelper.GetRange(1, 3); i++) {
                string tag = EventTags.Random();
                if (!ev.Tags.Contains(tag))
                    ev.Tags.Add(tag);
            }

            if (ev.Type == Event.KnownTypes.Error) {
                // limit error variation so that stacking will occur
                if (_randomErrors == null)
                    _randomErrors = new List<Error>(Enumerable.Range(1, 25).Select(i => GenerateError()));

                ev.Data[Event.KnownDataKeys.Error] = _randomErrors.Random();
            }

            // use server settings to see if we should include this data
            if (ExceptionlessClient.Default.Configuration.Settings.GetBoolean("IncludeConditionalData", true))
                ev.AddObject(new { Total = 32.34, ItemCount = 2, Email = "someone@somewhere.com" }, "Conditional Data");

            //ev.AddRecentTraceLogEntries();

            ExceptionlessClient.Default.SubmitEvent(ev);

            if (writeToConsole) {
                Console.SetCursorPosition(0, _optionsMenuLineCount + 2);
                Console.WriteLine("Sent 1 event.");
                Trace.WriteLine("Sent 1 event.");

                ClearNonOptionsLines();
            }
        }

        private static List<Error> _randomErrors;

        private static Error GenerateError(int maxErrorNestingLevel = 3, bool generateData = true, int currentNestingLevel = 0) {
            var error = new Error();
            error.Message = @"Generated exception message.";
            error.Type = ExceptionTypes.Random();
            if (RandomHelper.GetBool())
                error.Code = RandomHelper.GetRange(-234523453, 98690899).ToString();

            if (generateData) {
                for (int i = 0; i < RandomHelper.GetRange(1, 5); i++) {
                    string key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));
                    while (error.Data.ContainsKey(key) || key == Event.KnownDataKeys.Error)
                        key = RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 15));

                    error.Data.Add(key, RandomHelper.GetPronouncableString(RandomHelper.GetRange(5, 25)));
                }
            }

            var stack = new StackFrameCollection();
            for (int i = 0; i < RandomHelper.GetRange(1, 10); i++)
                stack.Add(GenerateStackFrame());
            error.StackTrace = stack;

            if (currentNestingLevel < maxErrorNestingLevel && RandomHelper.GetBool())
                error.Inner = GenerateError(maxErrorNestingLevel, generateData, currentNestingLevel + 1);

            return error;
        }

        private static Exceptionless.Models.Data.StackFrame GenerateStackFrame() {
            return new Exceptionless.Models.Data.StackFrame {
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

        private static void SendAllCapturedEventsFromDisk() {
            Console.SetCursorPosition(0, _optionsMenuLineCount + 2);
            Console.WriteLine("Sending captured events...");

            string path = Path.GetFullPath(@"..\..\Errors\");
            if (!Directory.Exists(path))
                return;

            int eventCount = 0;
            foreach (string file in Directory.GetFiles(path)) {
                var serializer = DependencyResolver.Default.GetJsonSerializer();
                var e = serializer.Deserialize<Event>(file);
                ExceptionlessClient.Default.SubmitEvent(e);

                eventCount++;
                Console.SetCursorPosition(0, _optionsMenuLineCount + 3);
                Console.WriteLine("Sent {0} events.", eventCount);
            }
        }

        public static readonly List<string> LogSources = new List<string> {
            "Some.Class",
            "MyClass",
            "CodeGenerator",
            "Exceptionless.Core.Parser.SomeClass"
        };

        public static readonly List<string> FeatureNames = new List<string> {
            "Feature1",
            "Feature2",
            "Feature3",
            "Feature4"
        };

        public static readonly List<string> PageNames = new List<string> {
            "/page1",
            "/page2",
            "/page3",
            "/page4"
        };

        public static readonly List<string> EventTypes = new List<string> {
            Event.KnownTypes.Error,
            Event.KnownTypes.FeatureUsage,
            Event.KnownTypes.Log,
            Event.KnownTypes.NotFound
        };

        public static readonly List<string> ExceptionTypes = new List<string> {
            "System.NullReferenceException",
            "System.ApplicationException",
            "System.AggregateException",
            "System.InvalidArgumentException",
            "System.InvalidOperationException"
        };

        public static readonly List<string> EventTags = new List<string> {
            "Tag1",
            "Tag2",
            "Tag3",
            "Tag4",
            "Tag5"
        };

        public static readonly List<string> Namespaces = new List<string> {
            "System",
            "System.IO",
            "CodeSmith",
            "CodeSmith.Generator",
            "SomeOther.Blah"
        };

        public static readonly List<string> TypeNames = new List<string> {
            "DateTime",
            "SomeType",
            "ProjectGenerator"
        };

        public static readonly List<string> MethodNames = new List<string> {
            "SomeMethod",
            "GenerateCode"
        };
    }
}