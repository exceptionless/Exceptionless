echo Running run.bat from directory: %cd%
SET jobsdir=..\..\jobs

IF NOT EXIST %jobsdir% (
  SET jobsdir=%WEBROOT_PATH%\App_Data\jobs\bin
  echo Job artifact directory not found.
)

FOR %%F IN (*Job.dll) DO (
 SET jobname=%%F
 goto done
)
:done

echo Copying job artifacts from: %jobsdir%
ROBOCOPY %jobsdir% .\ /S /NFL /NDL /NJH /NJS /nc /ns /np
IF %ERRORLEVEL% GEQ 8 exit 1

echo Running Job: %jobsdir%
dotnet %jobname% %*