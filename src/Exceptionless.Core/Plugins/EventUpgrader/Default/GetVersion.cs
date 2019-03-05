﻿using System;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    [Priority(0)]
    public class GetVersion : PluginBase, IEventUpgraderPlugin {
        public GetVersion(IOptions<AppOptions> options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) { }

        public void Upgrade(EventUpgraderContext ctx) {
            if (ctx.Version != null)
                return;

            if (ctx.Documents.Count == 0 || !ctx.Documents.First().HasValues) {
                ctx.Version = new Version();
                return;
            }

            var doc = ctx.Documents.First();
            if (!(doc["ExceptionlessClientInfo"] is JObject clientInfo) || !clientInfo.HasValues || clientInfo["Version"] == null) {
                ctx.Version = new Version();
                return;
            }

            if (clientInfo["Version"].ToString().Contains(" ")) {
                string version = clientInfo["Version"].ToString().Split(' ').First();
                ctx.Version = new Version(version);
                return;
            }

            if (clientInfo["Version"].ToString().Contains("-")) {
                string version = clientInfo["Version"].ToString().Split('-').First();
                ctx.Version = new Version(version);
                return;
            }

            // old version format
            ctx.Version = new Version(clientInfo["Version"].ToString());
        }
    }
}