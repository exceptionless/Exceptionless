#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading.Tasks;
using CodeSmith.Core.Scheduler;
using ServiceStack.Messaging;

namespace Exceptionless.Core.Jobs {
    public class StartMqJob : Job {
        private readonly IMessageService _messageService;

        public StartMqJob(IMessageService messageService) {
            _messageService = messageService;
        }

        public override Task<JobResult> RunAsync(JobRunContext context) {
            if (!String.Equals(_messageService.GetStatus(), "Disposed"))
                _messageService.Start();

            return Task.FromResult(new JobResult {
                Message = "Successfully started the message service."
            });
        }
    }
}