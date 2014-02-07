#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Scheduler;
using ServiceStack.Messaging;

namespace Exceptionless.Core.Jobs {
    public class StartMqJob : JobBase {
        private readonly IMessageService _messageService;

        public StartMqJob(IMessageService messageService) {
            _messageService = messageService;
        }

        public override JobResult Run(JobContext context) {
            if (!String.Equals(_messageService.GetStatus(), "Disposed"))
                _messageService.Start();

            return new JobResult {
                Result = "Successfully started the message service."
            };
        }
    }
}