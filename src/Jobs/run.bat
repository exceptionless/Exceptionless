@echo on

echo running run.bat from directory: %cd%
SET jobsdir=..\..\..\..\jobs

IF NOT EXIST %jobsdir% (
  SET jobsdir=%WEBROOT_PATH%\jobs
  echo Job artifact directory not found. Falling back to: %jobsdir%
)

FOR %%F IN (*Job.dll) DO (
 SET jobname=%%F
 goto done
)
:done

echo Copying job artifacts to: %jobsdir%
ROBOCOPY %jobsdir% .\ /S /NFL /NDL /NJH /NJS /nc /ns /np
IF %ERRORLEVEL% GEQ 8 exit 1

echo Running Job: %jobsdir%
dotnet %jobname% %*