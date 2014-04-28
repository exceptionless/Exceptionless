#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using AutoMapper;
using Exceptionless.Api.Models.Project;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Api {
    public class AutoMapperConfig {
        public static void CreateMappings() {
            Mapper.CreateMap<Project, ProjectInfoModel>().AfterMap((p, pi) => { pi.TimeZoneOffset = p.DefaultTimeZoneOffset().TotalMilliseconds; });
        }
    }
}