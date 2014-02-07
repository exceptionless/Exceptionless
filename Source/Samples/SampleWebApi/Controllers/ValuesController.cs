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
using System.Web.Http;

namespace Exceptionless.SampleWebApi.Controllers {
    public class ValuesController : ApiController {
        // GET api/values
        public IEnumerable<string> Get() {
            throw new ApplicationException("WebApi GET error");
        }

        // GET api/values/5
        public string Get(int id) {
            return "value";
        }

        // POST api/values
        public void Post([FromBody] string value) {
            throw new ApplicationException("WebApi POST error");
        }

        // PUT api/values/5
        public void Put(int id, [FromBody] string value) {}

        // DELETE api/values/5
        public void Delete(int id) {}
    }
}