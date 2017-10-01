using System;
using System.Threading.Tasks;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Services {
    public class SlackServiceTests : TestBase {
        private readonly Project _project;
        private readonly SlackService _slackService;

        public SlackServiceTests(ITestOutputHelper output) : base(output) {
            _slackService = GetService<SlackService>();
            _project = ProjectData.GenerateSampleProject();
            _project.Data[Project.KnownDataKeys.SlackToken] = new SlackToken {
                AccessToken = "MY KEY",
                IncomingWebhook = new SlackToken.IncomingWebHook {
                    Url = "MY Url"
                }
            };
        }

        [Fact]
        public Task SendEventNoticeSimpleErrorAsync() {
            var ex = GetException();
            return SendEventNoticeAsync(new PersistentEvent {
                Type = Event.KnownTypes.Error,
                Data = new Core.Models.DataDictionary {
                    {
                        Event.KnownDataKeys.SimpleError, new SimpleError {
                            Message = ex.Message,
                            Type = ex.GetType().FullName,
                            StackTrace = ex.StackTrace
                        }
                    }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeErrorAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Type = Event.KnownTypes.Error,
                Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Error, EventData.GenerateError() }
                }
            });
        }


        [Fact]
        public Task SendEventNoticeErrorWithDetailsAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Type = Event.KnownTypes.Error,
                Geo = "44.5241,-87.9056",
                ReferenceId = "ex_blake_dreams_of_cookies",
                Tags = new TagSet(new[] { "Out", "Of", "Cookies", "Critical" }),
                Count = 2,
                Value = 500,
                Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Error, EventData.GenerateError() },
                    { Event.KnownDataKeys.Version, "1.2.3" },
                    { Event.KnownDataKeys.UserInfo, new UserInfo("niemyjski", "Blake Niemyjski")  },
                    { Event.KnownDataKeys.UserDescription, new UserDescription("noreply@exceptionless.io", "Blake ate two boxes of cookies and needs help") }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeNotFoundAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Source = "[GET] /not-found?page=20",
                Type = Event.KnownTypes.NotFound
            });
        }

        [Fact]
        public Task SendEventNoticeFeatureAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Source = "My Feature Usage",
                Value = 1,
                Type = Event.KnownTypes.FeatureUsage
            });
        }

        [Fact]
        public Task SendEventNoticeEmptyLogEventAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Value = 1,
                Type = Event.KnownTypes.Log
            });
        }

        [Fact]
        public Task SendEventNoticeLogMessageAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "Only Message",
                Type = Event.KnownTypes.Log
            });
        }

        [Fact]
        public Task SendEventNoticeLogSourceAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Source = "Only Source",
                Type = Event.KnownTypes.Log
            });
        }

        [Fact]
        public Task SendEventNoticeLogReallyLongSourceAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Source = "Soooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooorce",
                Type = Event.KnownTypes.Log
            });
        }

        [Fact]
        public Task SendEventNoticeLogTraceMessageSourceLevelAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "My Trace Message",
                Source = "My Source",
                Type = Event.KnownTypes.Log,
                Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Level, "Trace" }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeLogInfoMessageSourceLevelAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "My Info Message",
                Source = "My Source",
                Type = Event.KnownTypes.Log,
                Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Level, "Info" }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeLogWarnMessageSourceLevelAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "My Warn Message",
                Source = "My Source",
                Type = Event.KnownTypes.Log,
                Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Level, "Warn" }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeLogErrorMessageSourceLevelAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "My Error Message",
                Source = "My Source",
                Type = Event.KnownTypes.Log,
                Data = new Core.Models.DataDictionary {
                    { Event.KnownDataKeys.Level, "Error" }
                }
            });
        }

        [Fact]
        public Task SendEventNoticeDefaultAsync() {
            return SendEventNoticeAsync(new PersistentEvent {
                Message = "Default Test Message",
                Source = "Default Test Source"
            });
        }

        private async Task SendEventNoticeAsync(PersistentEvent ev) {
            ev.Id = TestConstants.EventId;
            ev.OrganizationId = TestConstants.OrganizationId;
            ev.ProjectId = TestConstants.ProjectId;
            ev.StackId = TestConstants.StackId;
            ev.Date = SystemClock.OffsetUtcNow;

            await _slackService.SendEventNoticeAsync(ev, _project, RandomData.GetBool(), RandomData.GetBool(), 1);
            await RunWebHookJobAsync();
        }

        private Task RunWebHookJobAsync() {
            //if (!Settings.Current.EnableSlack)
            //    return Task.CompletedTask;

            var job = GetService<WebHooksJob>();
            return job.RunAsync();
        }

        private Exception GetException() {
            void TestInner()
            {
                void TestInnerInner()
                {
                    throw new ApplicationException("Random Test Exception");
                }

                TestInnerInner();
            }

            try {
                TestInner();
            }
            catch (Exception ex) {
                return ex;
            }

            return null;
        }
    }
}
