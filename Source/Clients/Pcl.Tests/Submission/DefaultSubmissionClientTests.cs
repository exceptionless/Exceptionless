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
using Exceptionless;
using Exceptionless.Models;
using Exceptionless.Submission;
using Xunit;

namespace Pcl.Tests.Submission {
    public class DefaultSubmissionClientTests {
        public DefaultSubmissionClientTests() {}

        [Fact]
        public void SubmitAsync() {
            var errors = new List<Error>() { new Error { Code = "Testing" }};
            var configuration = new Configuration {
                ServerUrl = "http://localhost:40000/api/v1/",
                ApiKey = "e3d51ea621464280bbcb79c11fd6483e"
            };

            var client = new DefaultSubmissionClient();
            var response = client.SubmitAsync(errors, configuration).Result;
            Assert.True(response.Success);
        }

        //[Fact]
        //public void MockHttpWebRequest() {
        //    // arrange
        //    var expected = "response content";
        //    var expectedBytes = Encoding.UTF8.GetBytes(expected);
        //    var responseStream = new MemoryStream();
        //    responseStream.Write(expectedBytes, 0, expectedBytes.Length);
        //    responseStream.Seek(0, SeekOrigin.Begin);

        //    var response = new Mock<HttpWebResponse>();
        //    response.Setup(c => c.GetResponseStream()).Returns(responseStream);

        //    var request = new Mock<HttpWebRequest>();
        //    request.Setup(c => c.GetResponse()).Returns(response.Object);

        //    var factory = new Mock<IHttpWebRequestFactory>();
        //    factory.Setup(c => c.Create(It.IsAny<string>())).Returns(request.Object);

        //    var requestCreator = new Mock<IWebRequestCreate>();
        //    requestCreator.Setup(c => c.Create(It.IsAny<Uri>())).Returns(request.Object);
        //    Assert.True(WebRequest.RegisterPrefix("http://", requestCreator.Object));
            

        //    // act
        //    var actualRequest = factory.Object.Create("http://www.google.com");
        //    actualRequest.Method = WebRequestMethods.Http.Get;

        //    string actual;

        //    using (var httpWebResponse = (HttpWebResponse)actualRequest.GetResponse()) {
        //        using (var streamReader = new StreamReader(httpWebResponse.GetResponseStream())) {
        //            actual = streamReader.ReadToEnd();
        //        }
        //    }

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //public interface IHttpWebRequestFactory {
        //    HttpWebRequest Create(string uri);
        //}
    }
}