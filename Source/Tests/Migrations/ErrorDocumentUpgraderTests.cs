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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Exceptionless.Core.Migrations.Documents;
using Exceptionless.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Tests.Migrations {
    public class ErrorDocumentUpgraderTests {
        public ErrorDocumentUpgraderTests() {
            RegisterUpgrades();
        }

        [Theory]
        [PropertyData("Errors")]
        public void EnsureBackwardsCompatibility(string errorFilePath) {
            JObject jObject = JObject.Parse(File.ReadAllText(errorFilePath));
            Assert.NotNull(jObject);

            DocumentUpgrader.Current.Upgrade<Error>(jObject);
            Assert.Null(Record.Exception(() => JsonConvert.DeserializeObject<Error>(jObject.ToString())));
        }

        [Theory]
        [PropertyData("Errors")]
        public void CheckForExpectedResults(string errorFilePath) {
            ErrorDocumentUpgrader.RegisterUpgrades();

            string expected = Regex.Replace(File.ReadAllText(Path.ChangeExtension(errorFilePath, ".expected.json")), @"\s", "");
            JObject jObject = JObject.Parse(File.ReadAllText(errorFilePath));
            Assert.NotNull(jObject);

            DocumentUpgrader.Current.Upgrade<Error>(jObject);
            Assert.Equal(Regex.Replace(jObject.ToString(), @"\s", ""), expected);
            Assert.Null(Record.Exception(() => JsonConvert.DeserializeObject<Error>(jObject.ToString())));
        }

        [Fact]
        public void UpgradeToLatestVersionTest() {
            Error_Version_1 error = Error_Version_1.Populate();
            Assert.Equal("1.0.0.1", error.ExceptionlessClientInfo.Version);

            string json = JsonConvert.SerializeObject(error);
            JObject jObject = JObject.Parse(json);

            DocumentUpgrader.Current.Upgrade<Error_Current_Version>(jObject);
            Assert.DoesNotContain("StartCount", jObject.ToString());
            Assert.DoesNotContain("SubmitCount", jObject.ToString());

            Assert.Null(Record.Exception(() => JsonConvert.DeserializeObject<Error_Version_1>(jObject.ToString())));
            Assert.Null(Record.Exception(() => JsonConvert.DeserializeObject<Error_Version_2>(jObject.ToString())));
            Assert.Null(Record.Exception(() => JsonConvert.DeserializeObject<Error_Current_Version>(jObject.ToString())));

            var latest = JsonConvert.DeserializeObject<Error_Current_Version>(jObject.ToString());
            Assert.Equal(1, latest.SubmissionCount);
            Assert.Equal(25, latest.PropertyValueIs25);
            Assert.NotEqual(DateTime.MinValue, latest.OccurrenceDate);

            Assert.NotNull(latest.ExceptionlessClientInfo);
            Assert.Equal("1.0.0.1", latest.ExceptionlessClientInfo.Version);
            Assert.NotEqual(DateTime.MinValue, latest.ExceptionlessClientInfo.InstallDate);
            Assert.NotEqual(Guid.Empty, latest.ExceptionlessClientInfo.InstallIdentifier);
        }

        private void RegisterUpgrades() {
            DocumentUpgrader.Current.Add<Error_Current_Version>(2, document => {
                var clientInfo = document["ExceptionlessClientInfo"] as JObject;
                if (clientInfo == null || clientInfo["Version"] == null)
                    return;

                int revison = new Version(clientInfo["Version"].ToString()).Revision;
                if (revison >= 2)
                    return;

                if (document["OccurrenceDate"] != null) {
                    DateTimeOffset date;
                    if (DateTimeOffset.TryParse(document["OccurrenceDate"].ToString(), out date)) {
                        document.Remove("OccurrenceDate");
                        document["OccurrenceDate"] = new JValue(date);
                    } else
                        document.Remove("OccurrenceDate");
                }
            });

            DocumentUpgrader.Current.Add<Error_Current_Version>(3, document => {
                var clientInfo = document["ExceptionlessClientInfo"] as JObject;
                if (clientInfo == null || clientInfo["Version"] == null)
                    return;

                int revison = new Version(clientInfo["Version"].ToString()).Revision;
                if (revison >= 3)
                    return;

                if (document["PropertyValueIs25"] == null)
                    document.Add("PropertyValueIs25", new JValue(25));

                if (document["InstallDate"] != null) {
                    DateTime date;
                    if (DateTime.TryParse(document["InstallDate"].ToString(), out date)) {
                        document.Remove("InstallDate");
                        document.Add("InstallDate", new JValue(new DateTimeOffset(date)));
                    } else
                        document.Remove("InstallDate");
                }

                if (document["SubmitCount"] != null) {
                    int number;
                    if (Int32.TryParse(document["SubmitCount"].ToString(), out number))
                        document.Add("SubmissionCount", new JValue(number));

                    document.Remove("SubmitCount");
                }

                if (clientInfo["InstallIdentifier"] != null) {
                    Guid guid;
                    if (Guid.TryParse(clientInfo["InstallIdentifier"].ToString(), out guid)) {
                        clientInfo.Remove("InstallIdentifier");
                        clientInfo.Add("InstallIdentifier", new JValue(guid));
                    } else
                        clientInfo.Remove("InstallIdentifier");
                }

                if (clientInfo["StartCount"] != null)
                    clientInfo.Remove("StartCount");
            });
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\ErrorData\", "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { file });

                return result.ToArray();
            }
        }
    }

    public class Error_Version_1 {
        public string Message { get; set; }
        public int SubmitCount { get; set; }
        public DateTime OccurrenceDate { get; set; }
        public ExceptionlessClientInfo_Version_1 ExceptionlessClientInfo { get; set; }

        public static Error_Version_1 Populate() {
            return new Error_Version_1 {
                Message = "Error Version 1",
                SubmitCount = 1,
                OccurrenceDate = DateTime.Now,
                ExceptionlessClientInfo = new ExceptionlessClientInfo_Version_1 {
                    Version = "1.0.0.1",
                    InstallIdentifier = Guid.NewGuid().ToString(),
                    InstallDate = DateTime.Now,
                    StartCount = 1
                }
            };
        }
    }

    public class ExceptionlessClientInfo_Version_1 {
        public string Version { get; set; }
        public string InstallIdentifier { get; set; }
        public DateTime InstallDate { get; set; }
        public int StartCount { get; set; }
    }

    public class Error_Version_2 {
        public string Message { get; set; }
        public int SubmitCount { get; set; }
        public DateTimeOffset OccurrenceDate { get; set; }
        public ExceptionlessClientInfo_Version_1 ExceptionlessClientInfo { get; set; }

        public static Error_Version_2 Populate() {
            return new Error_Version_2 {
                Message = "Error Version 2",
                SubmitCount = 1,
                OccurrenceDate = DateTime.Now,
                ExceptionlessClientInfo = new ExceptionlessClientInfo_Version_1 {
                    Version = "1.0.0.2",
                    InstallIdentifier = Guid.NewGuid().ToString(),
                    InstallDate = DateTime.Now
                }
            };
        }
    }

    public class Error_Current_Version {
        public string Message { get; set; }
        public int SubmissionCount { get; set; }
        public int PropertyValueIs25 { get; set; }
        public DateTimeOffset OccurrenceDate { get; set; }
        public ExceptionlessClientInfo_Current_Version ExceptionlessClientInfo { get; set; }

        public static Error_Current_Version Populate() {
            return new Error_Current_Version {
                Message = "Error Version 3",
                SubmissionCount = 1,
                PropertyValueIs25 = 25,
                OccurrenceDate = DateTime.Now,
                ExceptionlessClientInfo = new ExceptionlessClientInfo_Current_Version {
                    Version = "1.0.0.3",
                    InstallIdentifier = Guid.NewGuid(),
                    InstallDate = DateTime.Now
                }
            };
        }
    }

    public class ExceptionlessClientInfo_Current_Version {
        public string Version { get; set; }
        public Guid InstallIdentifier { get; set; }
        public DateTimeOffset InstallDate { get; set; }
    }
}