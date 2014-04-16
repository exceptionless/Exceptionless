#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using CodeSmith.Core.Scheduler;
using Exceptionless.Models;

namespace Exceptionless.Core.Jobs {
    public class MongoJobHistoryProvider : JobHistoryProvider {
        private readonly IJobHistoryRepository _jobHistoryRepository;

        public MongoJobHistoryProvider(IJobHistoryRepository repository) {
            _jobHistoryRepository = repository;
        }

        public override void RestoreHistory(JobRunner jobRunner) {
            var history = _jobHistoryRepository.Where(h => h.Name == jobRunner.Name).OrderByDescending(j => j.StartTime).FirstOrDefault();
            if (history == null)
                return;

            jobRunner.LastResult = history.Result;
            jobRunner.LastRunStartTime = history.StartTime;
            jobRunner.LastRunFinishTime = history.FinishTime;
            jobRunner.LastStatus = (JobStatus)history.Status;
        }

        public override void SaveHistory(JobRunner jobRunner) {
            var history = new JobHistory {
                Name = jobRunner.Name,
                StartTime = jobRunner.LastRunStartTime,
                FinishTime = jobRunner.LastRunFinishTime,
                Status = (int)jobRunner.LastStatus,
                Result = jobRunner.LastResult
            };

            _jobHistoryRepository.Add(history);
        }
    }

    public class JobHistory : IIdentity {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime FinishTime { get; set; }
        public int Status { get; set; }
        public string Result { get; set; }
    }
}