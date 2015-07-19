@echo off
%WEBROOT_PATH%\App_Data\JobRunner\Job.bat -t "Foundatio.Jobs.WorkItemJob, Foundatio" -c -s "Exceptionless.Core.Jobs.JobBootstrapper, Exceptionless.Core"