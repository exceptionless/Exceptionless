#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Samples;
using Xunit;

namespace Exceptionless.Tests.Stacking {
    public class ErrorSignatureTests {
        private readonly String[] _userNamespaces = new[] { "Exceptionless", "Samples" };
        private readonly String[] _commonMethods = new[] { "Exceptionless.Tests.Stacking.ErrorSignatureTests.CommonMethodA(Int32 x)", "Exceptionless.Tests.Stacking.ErrorSignatureTests.CommonMethodB" };

        [Fact]
        public void ExceptionWithExtras() {
            try {
                throw new WithExtrasException {
                    ErrorCode = 234423,
                    Number = 55,
                    SomeBool = true,
                    SomeEnum = SomeEnum.Value2
                };
            } catch (Exception ex) {
                Error ed = ex.ToErrorModel();

                var sig = new ErrorSignature(ed, _userNamespaces, _commonMethods);
                Assert.Equal("Exceptionless.Tests.Stacking.WithExtrasException", sig.SignatureInfo["ExceptionType"]);
                Assert.Equal("Exceptionless.Tests.Stacking.ErrorSignatureTests.ExceptionWithExtras()", sig.SignatureInfo["Method"]);
                Assert.Equal("55", sig.SignatureInfo["Number"]);
            }
        }

        [Fact]
        public void NestedExceptionSignature() {
            ApplicationException appEx;

            try {
                throw new Exception("iBland");
            } catch (Exception ex) {
                try {
                    throw new ArgumentException("iThrow", ex);
                } catch (Exception argEx) {
                    appEx = new ApplicationException("iWrap", argEx);
                }
            }

            Error ed = appEx.ToErrorModel();

            var sig = new ErrorSignature(ed, _userNamespaces);

            Assert.Equal("System.Exception", sig.SignatureInfo["ExceptionType"]);
            Assert.Equal("Exceptionless.Tests.Stacking.ErrorSignatureTests.NestedExceptionSignature()", sig.SignatureInfo["Method"]);
        }

        [Fact]
        public void CommonMethodTest() {
            try {
                CommonMethodA(5);
            } catch (Exception e) {
                Error ed = e.ToErrorModel();

                var sig = new ErrorSignature(ed, _userNamespaces, _commonMethods);
                Assert.Equal("System.Exception", sig.SignatureInfo["ExceptionType"]);
                Assert.Equal("Exceptionless.Tests.Stacking.ErrorSignatureTests.CommonMethodTest()", sig.SignatureInfo["Method"]);
            }
        }

        [Fact]
        public void FromDeepCallstackException() {
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

            var sig = new ErrorSignature(ed);

            Assert.Equal("System.ApplicationException", sig.SignatureInfo["ExceptionType"]);
            Assert.Equal("Samples.SampleErrors.SubMethod()", sig.SignatureInfo["Method"]);
        }

        [Fact]
        public void FromNestedClassException() {
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

            var sig = new ErrorSignature(ed);

            Assert.Equal("System.ApplicationException", sig.SignatureInfo["ExceptionType"]);
            Assert.Equal("Samples.SampleErrors.NestedErrorClass.NestedMethod()", sig.SignatureInfo["Method"]);
        }

        [Fact]
        public void FromGenericMethodException() {
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

            var sig = new ErrorSignature(ed);

            Assert.Equal("System.ApplicationException", sig.SignatureInfo["ExceptionType"]);
            Assert.Equal("Samples.SampleErrors.SomeGenericMethod[T,A](A blah)", sig.SignatureInfo["Method"]);
        }

        [Fact]
        public void FromLamda() {
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

            var sig = new ErrorSignature(ed);

            Assert.Equal("System.ApplicationException", sig.SignatureInfo["ExceptionType"]);
            Assert.Equal("Samples.SampleErrors.<ThrowExceptionFromLambda>b__0(String s, Int32 b)", sig.SignatureInfo["Method"]);
        }

        private void CommonMethodA(int x) {
            CommonMethodB(x);
        }

        private void CommonMethodB(int y) {
            throw new Exception("iCommon " + y);
        }
    }

    public class WithExtrasException : Exception {
        public int Number { get; set; }
        public int ErrorCode { get; set; }
        public string SomeString { get; set; }
        public bool SomeBool { get; set; }
        public SomeEnum SomeEnum { get; set; }
    }

    public enum SomeEnum {
        Value1,
        Value2
    }
}