#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.IO;
using System.Web;

namespace Samples {
    public static class SampleErrors {
        public static void ThrowException() {
            throw new ApplicationException();
        }

        public static void ThrowExceptionFromSystemCode() {
            string t = File.ReadAllText("file.txt");
        }

        public static int ThrowExceptionFromMethodWithParams(string b, int a) {
            throw new ApplicationException();
        }

        public static int ThrowExceptionFromMethodWithOutAndRefParams(string b, ref int a, out int c) {
            throw new ApplicationException();
        }

        public static void ThrowExceptionFromNestedMethod() {
            NestedErrorClass.NestedMethod();
        }

        public static void ThrowExceptionFromSubSubMethod() {
            ThrowExceptionFromSubMethod();
        }

        public static void ThrowExceptionGenericMethod() {
            SomeGenericMethod<string, int>(1);
        }

        public static void ThrowExceptionFromLambda() {
            var action = new Action<string, int>((s, b) => { throw new ApplicationException("Lambda"); });

            action("fdsfsd", 12);
        }

        public static void ThrowExceptionFromSecondLambda() {
            var action = new Action(() => { throw new ApplicationException("SecondLambda"); });
            action();
        }

        public static void ThrowExceptionFromSubMethod() {
            SubMethod();
        }

        private static void SubMethod() {
            // NOTE: This try catch was added to prevent method inlining.
            try {
                throw new ApplicationException();
            } catch {
                throw;
            }
        }

        private static T SomeGenericMethod<T, A>(A blah) {
            throw new ApplicationException("Generic method.");
        }

        public static void ThrowHttpUnhandledException() {
            try {
                throw new ApplicationException("The real issue.");
            } catch (Exception e) {
                throw new HttpUnhandledException("Wrapping the real issue.", e);
            }
        }

        public static class NestedErrorClass {
            public static void NestedMethod() {
                // NOTE: This try catch was added to prevent method inlining.
                try {
                    throw new ApplicationException();
                } catch {
                    throw;
                }
            }
        }
    }

    public class GenericErrorClass<T, B> {
        public T ThrowFromGenericClass(B b) {
            throw new ApplicationException();
        }
    }
}