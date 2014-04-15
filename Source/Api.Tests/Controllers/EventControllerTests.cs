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
using System.Net;
using System.Net.Http;
using System.Web.Http.Results;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Xunit;

namespace Exceptionless.Tests.Controllers {
    public class EventControllerTests {
        private readonly EventController _eventController = IoC.GetInstance<EventController>();

        public EventControllerTests(EventController controller) {}

        [Fact]
        public void Post() {
            var actionResult = _eventController.Post();
            Assert.True(actionResult.IsCompleted);
            Assert.False(actionResult.IsFaulted);
            Assert.False(actionResult.IsCanceled);
            Assert.IsType<OkResult>(actionResult.Result);
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\EventData\", "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { file });

                return result.ToArray();
            }
        }
    }
}