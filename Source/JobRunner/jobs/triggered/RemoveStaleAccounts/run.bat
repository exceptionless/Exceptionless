@echo off
if exist ..\..\..\JobRunner\job.exe (
	set JobRunner=..\..\..\JobRunner\job.exe
) else (
	set JobRunner=..\..\..\job.exe
)

%JobRunner% /jobtype:"Exceptionless.Core.Jobs.RemoveStaleAccountsJob, Exceptionless.Core"