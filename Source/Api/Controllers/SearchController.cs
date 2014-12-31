#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Http;
using Exceptionless.Core.Authorization;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/search")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class SearchController : ExceptionlessApiController {
        [HttpGet]
        [Route("validate")]
        public IHttpActionResult Validate(string query) {
            if (String.IsNullOrWhiteSpace(query))
                return Ok();

            // TODO: Validate this with a parser.
            if (query.Contains("\"") || query.StartsWith("{") || query.EndsWith(":") || query.EndsWith("}"))
                return BadRequest("Invalid character in search query.");

            return Ok();
        }
    }
}