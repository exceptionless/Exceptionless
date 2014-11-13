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
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace SampleConsole {
    internal class Program {
        private static bool _sendingContinuous = false;

        private static void Main() {

            ExceptionlessClient.Default.Startup();
            ExceptionlessClient.Default.Configuration.UseFolderStorage("store");
            ExceptionlessClient.Default.Configuration.UseFileLogger("store\\exceptionless.log");

            var tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            //ExceptionlessClient.Default.CreateLog("SampleConsole", "Some message.").AddObject(new { Blah = "Test" }, name: "Test Object").Submit();
            //ExceptionlessClient.Default.SubmitFeatureUsage("MyFeature");
            //ExceptionlessClient.Default.SubmitNotFound("/somepage");
            //ExceptionlessClient.Default.SubmitSessionStart(Guid.NewGuid().ToString("N"));
            ExceptionlessClient.Default.Configuration.AddEnrichment(ev => ev.Data[RandomHelper.GetPronouncableString(5)] = RandomHelper.GetPronouncableString(10));
            ExceptionlessClient.Default.Configuration.Settings.Changed += (sender, args) => Trace.WriteLine(String.Format("Action: {0} Key: {1} Value: {2}", args.Action, args.Item.Key, args.Item.Value ));

            while (true) {
                if (!_sendingContinuous) {
                    Console.Clear();
                    Console.WriteLine("1: Send 1\r\n2: Send 100\r\n3: Send 1 per second\r\n4: Send 10 per second\r\n5: Send 1,000\r\n6: Process queue\r\n7: Process directory\r\n\r\nQ: Quit");
                }

                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                Trace.WriteLine(String.Format("Key {0} pressed.", keyInfo.Key));

                if (keyInfo.Key == ConsoleKey.D1)
                    SendEvent();
                if (keyInfo.Key == ConsoleKey.D2)
                    SendContinuousEvents(50, token, 100);
                else if (keyInfo.Key == ConsoleKey.D3)
                    SendContinuousEvents(1000, token, maxDaysOld: 1);
                else if (keyInfo.Key == ConsoleKey.D4)
                    SendContinuousEvents(50, token);
                else if (keyInfo.Key == ConsoleKey.D5)
                    SendContinuousEvents(50, token, 1000);
                else if (keyInfo.Key == ConsoleKey.D6)
                    ExceptionlessClient.Default.ProcessQueue();
                else if (keyInfo.Key == ConsoleKey.D7)
                    SendAllCapturedEventsFromDisk();
                else if (keyInfo.Key == ConsoleKey.Q)
                    break;
                else if (keyInfo.Key == ConsoleKey.S) {
                    tokenSource.Cancel();
                    tokenSource = new CancellationTokenSource();
                    token = tokenSource.Token;
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        private static void SendContinuousEvents(int delay, CancellationToken token, int maxEvents = Int32.MaxValue, int maxDaysOld = 90) {
            _sendingContinuous = true;
            Console.WriteLine();
            Console.WriteLine("Press 's' to stop sending.");
            int eventCount = 0;

            Task.Factory.StartNew(delegate {
                while (eventCount < maxEvents) {
                    if (token.IsCancellationRequested) {
                        _sendingContinuous = false;
                        break;
                    }

                    SendEvent(false, maxDaysOld);
                    eventCount++;

                    Console.SetCursorPosition(0, 13);
                    Console.WriteLine("Sent {0} events.", eventCount);
                    Trace.WriteLine(String.Format("Sent {0} events.", eventCount));

                    Thread.Sleep(delay);
                }
            }, token);
        }

        private static void SendEvent(bool writeToConsole = true, int maxDaysOld = 90) {
            var ev = new Event();
            ev.Date = RandomHelper.GetDateTime(DateTime.Now.AddDays(-maxDaysOld), DateTime.Now);

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

            // use server config to see if we should include this data
            if (ExceptionlessClient.Default.Configuration.Settings.GetBoolean("IncludeConditionalData"))
                ev.AddObject(new { Total = 32.34, ItemCount = 2, Email = "someone@somewhere.com" }, "Conditional Data");

            //ev.AddRecentTraceLogEntries();

            ExceptionlessClient.Default.SubmitEvent(ev);

            if (writeToConsole) {
                Console.SetCursorPosition(0, 11);
                Console.WriteLine("Sent 1 event.");
                Trace.WriteLine("Sent 1 event.");
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
            string path = Path.GetFullPath(@"..\..\Errors\");
            if (!Directory.Exists(path))
                return;

            foreach (string file in Directory.GetFiles(path)) {
                var serializer = DependencyResolver.Default.GetJsonSerializer();
                var e = serializer.Deserialize<Event>(file);
                ExceptionlessClient.Default.SubmitEvent(e);
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