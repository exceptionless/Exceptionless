using AutoMapper.Internal;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class Request {
        public string HostName { get; set; }

        public string Url { get; set; }

        public string HttpMethod { get; set; }

        public string IPAddress { get; set; }

        public IDictionary QueryString { get; set; }

        public IList Cookies { get; set; }

        public IDictionary Form { get; set; }

        public IDictionary Data { get; set; }

        public IDictionary Headers { get; set; }

        public string RawData { get; set; }
    }
}
