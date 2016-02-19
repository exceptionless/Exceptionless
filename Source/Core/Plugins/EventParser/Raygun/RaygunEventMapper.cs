using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Newtonsoft.Json;

namespace Exceptionless.Core.Plugins.EventParser.Raygun {
    public class RaygunEventMapper : EventMapperBase<RaygunModel> {
        protected override PersistentEvent CreateEvent(RaygunModel source) {
            var ev = base.CreateEvent(source);
            ev.Date = source.OccurredOn;
            ev.Type = Event.KnownTypes.Error;

            var details = source.Details;
            if (details != null) {
                if (details.Tags != null)
                    ev.Tags.AddRange(details.Tags);

                //if (!String.IsNullOrEmpty(details.GroupingKey))
                //    ev.SetManualStackingKey(details.GroupingKey);

                if (details.UserCustomData != null)
                    foreach (var kvp in details.UserCustomData)
                        ev.Data[kvp.Key] = kvp.Value;

                if (details.Client != null)
                    ev.Data[nameof(details.Client)] = details.Client;

                if (details.Response != null)
                    ev.Data[nameof(details.Response)] = details.Response;

                if (!String.IsNullOrEmpty(details.Error?.Message))
                    ev.Message = details.Error.Message;
            }

            return ev;
        }

        protected override Models.Data.Error MapError(RaygunModel source) {
            var sourceError = source?.Details?.Error;
            if (sourceError == null)
                return null;

            var error = new Models.Data.Error {
                TargetMethod = MapTargetMethod(sourceError),
                Message = sourceError.Message,
                Type = sourceError.ClassName,
                StackTrace = MapStackFrames(sourceError.StackTrace),
                Inner = MapInnerError(sourceError.InnerError)
            };

            if (sourceError.Data != null)
                error.Data.AddRange(sourceError.Data);

            return error;
        }

        protected override EnvironmentInfo MapEnvironmentInfo(RaygunModel source) {
            var details = source?.Details;
            if (details == null)
                return null;

            var ei = new EnvironmentInfo { MachineName = details.MachineName };
            var environment = details.Environment;
            if (environment == null)
                return ei;

            ei.Architecture = environment.Architecture;
            ei.AvailablePhysicalMemory = NormalizeMemory(environment.AvailablePhysicalMemory, details.Client);
            //ei.CommandLine;
            ei.InstallId = details.User?.Uuid;
            //ei.IpAddress;
            ei.OSName = environment.Platform;
            ei.OSVersion = environment.OsVersion;
            //ei.ProcessId;
            //ei.ProcessMemorySize;
            //ei.ProcessName;
            ei.ProcessorCount = environment.ProcessorCount;
            //ei.RuntimeVersion;
            //ei.ThreadId;
            //ei.ThreadName;
            ei.TotalPhysicalMemory = NormalizeMemory(environment.TotalPhysicalMemory, details.Client);

            // Additional Fields
            if (environment.AvailableVirtualMemory > 0)
                ei.Data[nameof(environment.AvailableVirtualMemory)] = NormalizeMemory(environment.AvailableVirtualMemory, details.Client);
            if (!String.IsNullOrEmpty(environment.Browser))
                ei.Data[nameof(environment.Browser)] = environment.Browser;
            if (!String.IsNullOrEmpty(environment.BrowserName))
                ei.Data[nameof(environment.BrowserName)] = environment.BrowserName;
            if (!String.IsNullOrEmpty(environment.BrowserVersion))
                ei.Data[nameof(environment.BrowserVersion)] = environment.BrowserVersion;
            if (environment.BrowserHeight > 0)
                ei.Data[nameof(environment.BrowserHeight)] = environment.BrowserHeight;
            if (environment.BrowserWidth > 0)
                ei.Data[nameof(environment.BrowserWidth)] = environment.BrowserWidth;
            if (environment.ColorDepth > 0)
                ei.Data[nameof(environment.ColorDepth)] = environment.ColorDepth;
            if (!String.IsNullOrEmpty(environment.Cpu))
                ei.Data[nameof(environment.Cpu)] = environment.Cpu;
            if (!String.IsNullOrEmpty(environment.CurrentOrientation))
                ei.Data[nameof(environment.CurrentOrientation)] = environment.CurrentOrientation;
            if (!String.IsNullOrEmpty(environment.DeviceManufacturer))
                ei.Data[nameof(environment.DeviceManufacturer)] = environment.DeviceManufacturer;
            if (!String.IsNullOrEmpty(environment.DeviceName))
                ei.Data[nameof(environment.DeviceName)] = environment.DeviceName;
            if (environment.DiskSpaceFree?.Count > 0)
                ei.Data[nameof(environment.DiskSpaceFree)] = environment.DiskSpaceFree;
            if (!String.IsNullOrEmpty(environment.Locale))
                ei.Data[nameof(environment.Locale)] = environment.Locale;
            if (!String.IsNullOrEmpty(environment.Model))
                ei.Data[nameof(environment.Model)] = environment.Model;
            if (!String.IsNullOrEmpty(environment.PackageVersion))
                ei.Data[nameof(environment.PackageVersion)] = environment.PackageVersion;
            if (environment.ResolutionScale > 0)
                ei.Data[nameof(environment.ResolutionScale)] = environment.ResolutionScale;
            if (environment.ScreenHeight > 0)
                ei.Data[nameof(environment.ScreenHeight)] = environment.ScreenHeight;
            if (environment.ScreenWidth > 0)
                ei.Data[nameof(environment.ScreenWidth)] = environment.ScreenWidth;
            if (environment.TotalVirtualMemory > 0)
                ei.Data[nameof(environment.TotalVirtualMemory)] = NormalizeMemory(environment.TotalVirtualMemory, details.Client);
            if (environment.UtcOffset > 0)
                ei.Data[nameof(environment.UtcOffset)] = environment.UtcOffset;
            if (environment.WindowBoundsHeight > 0)
                ei.Data[nameof(environment.WindowBoundsHeight)] = environment.WindowBoundsHeight;
            if (environment.WindowBoundsWidth > 0)
                ei.Data[nameof(environment.WindowBoundsWidth)] = environment.WindowBoundsWidth;

            return ei;
        }

        protected override RequestInfo MapRequestInfo(RaygunModel source) {
            var request = source?.Details?.Request;
            if (request == null)
                return null;

            var environment = source.Details?.Environment;
            var ri = new RequestInfo {
                ClientIpAddress = request.IPAddress,
                //Cookies
                Host = request.HostName,
                HttpMethod = request.HttpMethod,
                IsSecure = request.HostName?.StartsWith("https") ?? false,
                Path = request.Url ?? request.GetHeaderValue("PATH_INFO"),
                PostData = request.Form.Any() ? request.Form : null,
                Referrer = request.GetHeaderValue("Referer") ?? request.GetHeaderValue("HTTP_REFERER"),
                UserAgent = request.GetHeaderValue("HTTP_USER_AGENT") ?? request.GetHeaderValue("USER-AGENT") ?? environment.BrowserVersion,
                QueryString = request.QueryString as Dictionary<string, string>
            };

            int port;
            Uri uri;
            if (Int32.TryParse(request.GetHeaderValue("SERVER_PORT"), out port))
                ri.Port = port;
            else if (Uri.TryCreate(request.HostName, UriKind.RelativeOrAbsolute, out uri))
                ri.Port = uri.Port;
            else
                ri.Port = 80;

            if (request.Headers != null)
                ri.Data[nameof(request.Headers)] = request.Headers;

            if (!String.IsNullOrEmpty(request.RawData))
                ri.Data[nameof(request.RawData)] = request.RawData;

            // TODO: [Discussion] Should we bring in our own user agent parser and parse the user agent or rely on there stuff?
            if (!String.IsNullOrEmpty(environment?.Browser))
                ri.Data[RequestInfo.KnownDataKeys.Browser] = environment.Browser;
            if (!String.IsNullOrEmpty(environment?.BrowserVersion))
                ri.Data[RequestInfo.KnownDataKeys.BrowserVersion] = environment.BrowserVersion;
            if (environment?.BrowserHeight > 0)
                ri.Data[nameof(environment.BrowserHeight)] = environment.BrowserHeight;
            if (environment?.BrowserWidth > 0)
                ri.Data[nameof(environment.BrowserWidth)] = environment.BrowserWidth;

            if (!String.IsNullOrEmpty(environment?.DeviceName)) {
                if (!String.IsNullOrEmpty(environment.DeviceManufacturer))
                    ri.Data[RequestInfo.KnownDataKeys.Device] = $"{environment.DeviceManufacturer} {environment.DeviceName}";
                else
                    ri.Data[RequestInfo.KnownDataKeys.Device] = environment.DeviceName;
            }

            return ri;
        }

        protected override UserInfo MapUserInfo(RaygunModel source) {
            var user = source?.Details?.User;
            if (user == null)
                return null;

            var ui = new UserInfo {
                Identity = user.Email,
                Name = user.FullName
            };

            // NOTE: We try and set the users id to email in our system (and index it as an email address). Should we set it to the user id? 
            ui.Data[nameof(user.Email)] = user.Email;
            ui.Data[nameof(user.FirstName)] = user.FirstName;
            ui.Data[nameof(user.FullName)] = user.FullName;
            ui.Data[nameof(user.Identifier)] = user.Identifier;
            ui.Data[nameof(user.IsAnonymous)] = user.IsAnonymous;
            ui.Data[nameof(user.Uuid)] = user.Uuid;

            return ui;
        }

        protected override string MapVersion(RaygunModel source) {
            return source.Details?.Version;
        }
        
        private InnerError MapInnerError(Error error) {
            if (error?.StackTrace == null)
                return null;

            var innerError = new InnerError {
                TargetMethod = MapTargetMethod(error),
                Message = error.Message,
                Type = error.ClassName,
                StackTrace = MapStackFrames(error.StackTrace),
                Inner = MapInnerError(error.InnerError)
            };

            if (error.Data != null)
                innerError.Data.AddRange(error.Data);

            return innerError;
        }

        private StackFrameCollection MapStackFrames(IList<StackTrace> stackTraces) {
            var frames = new StackFrameCollection();

            // raygun seems to put one fake element when there's no stacktrace at all. Try to detect this fake element
            // and return an empty collection instead.
            if (stackTraces.Count == 1 && stackTraces.First().FileName == "none")
                return frames;

            foreach (var stackTrace in stackTraces) {
                var di = GetDeclaringInfo(stackTrace.ClassName);
                var frame = new StackFrame {
                    DeclaringType = di.Item1,
                    DeclaringNamespace = di.Item2,
                    Name = GetMethodNameWithoutParameter(stackTrace.MethodName),
                    LineNumber = stackTrace.LineNumber,
                    Column = stackTrace.ColumnNumber,
                    FileName = stackTrace.FileName,
                    ModuleId = -1
                };

                // TODO Fill in generics and parameter info.
                frames.Add(frame);
            }

            return frames;
        }

        private Method MapTargetMethod(Error error) {
            var firstFrame = error.StackTrace?.FirstOrDefault();
            if (firstFrame == null)
                return null;

            // TODO Fill in generics and parameter info.
            var di = GetDeclaringInfo(firstFrame.ClassName);
            return new Method {
                DeclaringType = di.Item1,
                DeclaringNamespace = di.Item2,
                Name = GetMethodNameWithoutParameter(firstFrame.MethodName),
                ModuleId = -1
            };
        }

        private Tuple<string, string> GetDeclaringInfo(string className) {
            if (String.IsNullOrEmpty(className))
                return new Tuple<string, string>(null, null);

            string declaringType = null;
            string declaringNamespace = null;
            int lastDotIndex = className.LastIndexOf('.');

            if (lastDotIndex == -1) {
                declaringType = className;
            } else {
                declaringType = className.Substring(lastDotIndex + 1);
                declaringNamespace = className.Substring(0, lastDotIndex);
            }

            // raygun seems to put the word "(unknown)" when there's no declaringType. We catch that and we put
            // null instead.
            if (declaringType == "(unknown)")
                declaringType = null;

            return new Tuple<string, string>(declaringType, declaringNamespace);
        }

        private string GetMethodNameWithoutParameter(string methodName) {
            if (String.IsNullOrEmpty(methodName))
                return null;

            string methodNameWithoutParameter;
            int firstBracketIndex = methodName.IndexOf('(');
            if (firstBracketIndex == -1)
                methodNameWithoutParameter = methodName;
            else
                methodNameWithoutParameter = methodName.Substring(0, firstBracketIndex);

            return methodNameWithoutParameter;
        }

        /// <summary>
        /// Someone never normalized the memory to one specific size (bytes, MB, etc..).
        /// </summary>
        private long NormalizeMemory(ulong total, Client client) {
            if (String.Equals(client.Name, "raygun-node"))
                return (long)total;

            // Normalize MB to bytes.
            return (long)(total * 1048576);
        }
    }

    #region Models

    public class RaygunModel {
        public DateTimeOffset OccurredOn { get; set; }

        public Details Details { get; set; }
    }

    public class Details {
        public string MachineName { get; set; }

        public string GroupingKey { get; set; }

        public string Version { get; set; }

        public Client Client { get; set; }

        public Error Error { get; set; }

        public Environment Environment { get; set; }

        public IList<string> Tags { get; set; }

        public IDictionary<string, object> UserCustomData { get; set; }

        public Request Request { get; set; }

        public Response Response { get; set; }

        public User User { get; set; }
    }

    public class Client {
        public string Name { get; set; }

        public string Version { get; set; }

        public string ClientUrl { get; set; }
    }

    public class Error {
        public Error InnerError { get; set; }

        public IDictionary<string, object> Data { get; set; }

        public string ClassName { get; set; }

        public string Message { get; set; }

        public IList<StackTrace> StackTrace { get; set; }
    }

    public class StackTrace {
        public int LineNumber { get; set; }

        public string ClassName { get; set; }

        public int ColumnNumber { get; set; }

        public string FileName { get; set; }

        public string MethodName { get; set; }
    }

    public class Environment {
        public int ProcessorCount { get; set; }

        public string OsVersion { get; set; }

        public double WindowBoundsWidth { get; set; }

        public double WindowBoundsHeight { get; set; }

        [JsonProperty("browser-Width")]
        public int BrowserWidth { get; set; }

        [JsonProperty("browser-Height")]
        public int BrowserHeight { get; set; }

        [JsonProperty("screen-Width")]
        public int ScreenWidth { get; set; }

        [JsonProperty("screen-Height")]
        public int ScreenHeight { get; set; }

        public double ResolutionScale { get; set; }

        [JsonProperty("color-Depth")]
        public int ColorDepth { get; set; }

        public string CurrentOrientation { get; set; }

        public string Cpu { get; set; }

        public string PackageVersion { get; set; }

        public string Architecture { get; set; }

        public string DeviceManufacturer { get; set; }

        public string Model { get; set; }

        public ulong TotalPhysicalMemory { get; set; }

        public ulong AvailablePhysicalMemory { get; set; }

        public ulong TotalVirtualMemory { get; set; }

        public ulong AvailableVirtualMemory { get; set; }

        public IList<double> DiskSpaceFree { get; set; }

        public string DeviceName { get; set; }

        public string Locale { get; set; }

        public double UtcOffset { get; set; }

        public string Browser { get; set; }

        public string BrowserName { get; set; }

        [JsonProperty("browser-Version")]
        public string BrowserVersion { get; set; }

        public string Platform { get; set; }
    }

    public class Request {
        public string HostName { get; set; }

        public string Url { get; set; }

        public string HttpMethod { get; set; }

        public string IPAddress { get; set; }

        public IDictionary<string, string> QueryString { get; set; }

        public IDictionary<string, object> Form { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public string RawData { get; set; }

        public string GetHeaderValue(string key) {
            var kvp = Headers?.FirstOrDefault(h => String.Equals(h.Key, key, StringComparison.OrdinalIgnoreCase));
            return kvp?.Value;
        }
    }

    public class Response {
        public int StatusCode { get; set; }

        public string StatusDescription { get; set; }
    }

    public class User {
        public string Identifier { get; set; }

        public bool IsAnonymous { get; set; }

        public string Email { get; set; }

        public string FullName { get; set; }

        public string FirstName { get; set; }

        public string Uuid { get; set; }
    }

    #endregion
}
