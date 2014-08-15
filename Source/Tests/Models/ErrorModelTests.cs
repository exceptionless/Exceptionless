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
using System.Text.RegularExpressions;
using Exceptionless.Core.Extensions;
using Exceptionless.Models.Data;
using Exceptionless.Tests.Stacking;
using Samples;
using Xunit;

namespace Exceptionless.Tests.Models {
    public class ErrorModelTests {
        [Fact]
        public void MarkAsCriticalException() {
            Error model = new Exception().ToErrorModel();
            Assert.Equal(0, model.Tags.Count);
            model.MarkAsCritical();
            Assert.Equal(1, model.Tags.Count);
            Assert.True(model.Tags.Contains("Critical"));
        }

        [Fact]
        public void PopulateFromException() {
            try {
                throw new WithExtrasException {
                    ErrorCode = 234423,
                    Number = 55,
                    SomeBool = true,
                    SomeEnum = SomeEnum.Value2
                };
            } catch (Exception ex) {
                Error ed = ex.ToErrorModel();
                Compare(ex, ed);
            }
        }

        [Fact]
        public void ValidateExceptionTitle() {
            const string messageFormat = "Exception of type '{0}' was thrown.";

            Error model = new Exception().ToErrorModel();
            Assert.Equal(String.Format(messageFormat, "System.Exception"), model.Message);

            model = new Exception(null, new Exception(null)).ToErrorModel();
            Assert.Equal(String.Format(messageFormat, "System.Exception"), model.Message);
            Assert.NotNull(model.Inner);
            Assert.Equal(String.Format(messageFormat, "System.Exception"), model.Inner.Message);

            var testCases = new Dictionary<string, string> {
                { "", String.Format(messageFormat, "System.Exception") },
                { " ", String.Format(messageFormat, "System.Exception") },
                { "    Test  Exception         Message         ", "Test Exception Message" },
                { "    Test  Exception  \t       Message         ", "Test Exception Message" },
                { "    Test  Exception  \t       Message    \r\n\t    Results ", "Test Exception Message Results" }
            };

            foreach (var testCase in testCases) {
                model = new Exception(testCase.Key, new Exception(testCase.Key)).ToErrorModel();
                Assert.Equal(testCase.Value, model.Message);
                Assert.NotNull(model.Inner);
                Assert.Equal(testCase.Value, model.Inner.Message);
            }
        }

        [Fact]
        public void PopulateFromNestedException() {
            ApplicationException appEx = null;

            try {
                throw new Exception("iBland");
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Assert.NotNull(appEx);

            Error ed = appEx.ToErrorModel();

            Compare(appEx, ed);
        }

        [Fact]
        public void PopulateFromDeepCallstackException() {
            ApplicationException appEx = null;

            try {
                SampleErrors.ThrowExceptionFromSubSubMethod();
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Assert.NotNull(appEx);

            Error ed = appEx.ToErrorModel();

            Compare(appEx, ed);
        }

        [Fact]
        public void PopulateFromNestedClassException() {
            ApplicationException appEx = null;

            try {
                SampleErrors.ThrowExceptionFromNestedMethod();
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Assert.NotNull(appEx);

            Error ed = appEx.ToErrorModel();

            Compare(appEx, ed);
        }

        [Fact]
        public void PopulateFromGenericMethodException() {
            ApplicationException appEx = null;

            try {
                SampleErrors.ThrowExceptionGenericMethod();
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Assert.NotNull(appEx);

            Error ed = appEx.ToErrorModel();

            Compare(appEx, ed);
        }

        [Fact]
        public void PopulateFromMethodWithOutAndRefParams() {
            ApplicationException appEx = null;

            try {
                int a = 1, b;
                SampleErrors.ThrowExceptionFromMethodWithOutAndRefParams("sdfs", ref a, out b);
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Assert.NotNull(appEx);

            Error ed = appEx.ToErrorModel();

            Compare(appEx, ed);
        }

        [Fact]
        public void PopulateFromMethodWithParams() {
            ApplicationException appEx = null;

            try {
                SampleErrors.ThrowExceptionFromMethodWithParams("sdfs", 1);
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Assert.NotNull(appEx);

            Error ed = appEx.ToErrorModel();

            Compare(appEx, ed);
        }

        [Fact]
        public void PopulateFromGenericClass() {
            ApplicationException appEx = null;

            try {
                var c = new GenericErrorClass<string, int>();
                c.ThrowFromGenericClass(12);
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Assert.NotNull(appEx);

            Error ed = appEx.ToErrorModel();

            Compare(appEx, ed);
        }

        [Fact]
        public void PopulateFromLamda() {
            ApplicationException appEx = null;

            try {
                SampleErrors.ThrowExceptionFromLambda();
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Assert.NotNull(appEx);

            Error ed = appEx.ToErrorModel();

            Compare(appEx, ed);
        }

        private void Compare(Exception ex, InnerError err, int level = 1) {
            Console.WriteLine("Level " + level);
            Assert.Equal(ex.GetType().FullName, err.Type);
            if (String.IsNullOrEmpty(ex.StackTrace))
                Assert.Equal(0, err.StackTrace.Count);
            else {
                string[] lines = Regex.Split(ex.StackTrace, "\r\n|\r|\n");
                Assert.Equal(lines.Length, err.StackTrace.Count);
                Assert.Equal(ex.StackTrace, err.StackTrace.ToString());
            }

            // TODO: Fix formatting bugs with inner exception tostring
            if (level == 1)
                Assert.Equal(ex.ToString(), err.ToString());

            if (ex.InnerException != null)
                Compare(ex.InnerException, err.Inner, level + 1);
        }
    }
}