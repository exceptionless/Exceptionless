#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Nancy;

namespace Exceptionless.SampleNancy {
    public class ExceptionlessModule : NancyModule {
        public ExceptionlessModule() {
            Get["/"] = _ => "Hello!";
            Get["/error"] = _ => { throw new Exception("Unhandled Exception"); };
            Get["/custom"] = _ => {
                new Exception("Handled Exception").ToExceptionless().AddRequestInfo(Context).Submit();
                return "ok, handled";
            };
        }
    }
}
