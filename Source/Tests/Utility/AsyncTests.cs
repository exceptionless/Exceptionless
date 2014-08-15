#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Repositories;
using Xunit;

namespace Exceptionless.Tests.Utility {
    public class AsyncTests {
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly IDayStackStatsRepository _dayStackStats = IoC.GetInstance<IDayStackStatsRepository>();
        private readonly IMonthStackStatsRepository _monthStackStats = IoC.GetInstance<IMonthStackStatsRepository>();
        private readonly IDayProjectStatsRepository _dayProjectStats = IoC.GetInstance<IDayProjectStatsRepository>();
        private readonly IMonthProjectStatsRepository _monthProjectStats = IoC.GetInstance<IMonthProjectStatsRepository>();

        [Fact(Skip = "This is only for experimentation.")]
        public void CanRunAsyncMethodsAndWait() {
            string taskId = Guid.NewGuid().ToString("N");
            //_cacheClient.Set(taskId, 0);

            string id = "51cf1b597841550f1c44b539";
            var tasks = new[] {
                _stackRepository.RemoveAllByProjectIdAsync(id),
                _eventRepository.RemoveAllByProjectIdAsync(id),
                _dayStackStats.RemoveAllByProjectIdAsync(id),
                _monthStackStats.RemoveAllByProjectIdAsync(id),
                _dayProjectStats.RemoveAllByProjectIdAsync(id),
                _monthProjectStats.RemoveAllByProjectIdAsync(id)
            };

            try {
                Task.WaitAll(tasks);
                // wait 2 minutes after completed and then remove the task id
                //Task.Factory.StartNewDelayed(2 * 60 * 1000, () => _cacheClient.Remove(taskId));
            } catch (Exception) {
                //Log.Error().Project(id).Exception(e).Message("Error resetting project data.").Report().Write();
            }
        }

        public async Task DoWorkAsync() {
            await Task.Run(() => Thread.Sleep(5000));
        }

        private void TaskFaulted(Task task, string taskId) {
            Debug.WriteLine("Task faulted.");
        }

        private void TaskCompleted(string taskId) {
            Debug.WriteLine("Task completed {0}.", taskId);
            //const decimal taskCount = 6m;
            //const decimal taskProgress = 100m / taskCount;

            //var value = _cacheClient.Get<int>(taskId);
            //value += (int)taskProgress;
            //if (value > 100)
            //    value = 100;

            //_cacheClient.Set(taskId, value);
        }
    }
}