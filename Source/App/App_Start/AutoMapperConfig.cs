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
using Exceptionless.App.Models.Error;
using Exceptionless.App.Models.Organization;
using Exceptionless.App.Models.Project;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Models;
using Stripe;

namespace Exceptionless.App {
    public class AutoMapperConfig {
        public static void CreateMappings() {
            Mapper.CreateMap<Error, ErrorModel>().AfterMap((e, em) => {
                em.ClientTime = e.OccurrenceDate;
                em.PopulateExtraInfo();
            });

            Mapper.CreateMap<Error, ErrorResult>()
                .ForMember(er => er.Date, opt => opt.MapFrom(e => e.OccurrenceDate))
                .AfterMap((e, er) => {
                    StackingInfo info = e.GetStackingInfo();
                    er.Type = info.FullTypeName;
                    er.Method = info.Method != null ? info.Method.FullName : null;
                    er.Path = info.Path;
                    er.Is404 = info.Is404;
                    er.Message = info.Message;
                });

            Mapper.CreateMap<Stack, ErrorStackResult>()
                .ForMember(esr => esr.First, opt => opt.MapFrom(es => es.FirstOccurrence))
                .ForMember(esr => esr.Last, opt => opt.MapFrom(es => es.LastOccurrence))
                .ForMember(esr => esr.Total, opt => opt.MapFrom(es => es.TotalOccurrences))
                .AfterMap((es, esr) => {
                    esr.Type = es.SignatureInfo.ContainsKey("ExceptionType") ? es.SignatureInfo["ExceptionType"] : null;
                    esr.Method = es.SignatureInfo.ContainsKey("Method") ? es.SignatureInfo["Method"] : null;
                    esr.Path = es.SignatureInfo.ContainsKey("Path") ? es.SignatureInfo["Path"] : null;
                    esr.Is404 = es.SignatureInfo.ContainsKey("Path") && !es.SignatureInfo.ContainsKey("ExceptionType");
                });

            Mapper.CreateMap<Project, ProjectModel>();

            Mapper.CreateMap<StripeInvoice, InvoiceGridModel>().AfterMap((si, igm) => { igm.Id = si.Id.Substring(3); });

            Mapper.CreateMap<Project, ProjectInfoModel>().AfterMap((p, pi) => { pi.TimeZoneOffset = p.DefaultTimeZoneOffset().TotalMilliseconds; });
        }
    }
}