#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net.Http;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using Owin;

namespace Exceptionless.SampleWebApi {
    public class Startup {
        public void Configuration(IAppBuilder app) {
            var config = new HttpConfiguration();
            config.Routes.MapHttpRoute(name: "DefaultApi", routeTemplate: "api/{controller}/{id}", defaults: new { id = RouteParameter.Optional });
            app.UseWebApi(config);

            ExceptionlessClient.Default.Configuration.UseTraceLogger();
            ExceptionlessClient.Default.RegisterWebApi(config);
        }
    }

    public class Program {
        private static void Main() {
            string baseAddress = "http://localhost:9000/";

            using (WebApp.Start<Startup>(url: baseAddress)) {
                Console.WriteLine("Press any key to send a request...");
                ConsoleKeyInfo key = Console.ReadKey();
                while (key.KeyChar != 27) {
                    // Create HttpCient and make a request to api/values 
                    var client = new HttpClient();
                    HttpResponseMessage response = client.GetAsync(baseAddress + "api/values").Result;

                    Console.WriteLine(response);
                    Console.WriteLine(response.Content.ReadAsStringAsync().Result);

                    key = Console.ReadKey();
                }
            }
        }
    }
}