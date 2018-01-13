@echo off

SET jobsdir=..\..\..\..\jobs

IF NOT EXIST %jobsdir% (
  SET jobsdir=%WEBROOT_PATH%\jobs
)

FOR %%F IN (*Job.dll) DO (
 SET jobname=%%F
 goto done
)
:done

robocopy %jobsdir% .\ /S /NFL /NDL /NJH /NJS /nc /ns /np
IF %ERRORLEVEL% GEQ 8 exit 1

dotnet %jobname% %*