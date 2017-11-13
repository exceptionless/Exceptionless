@echo off

SET bindir=..\..\..\bin

IF NOT EXIST %bindir% (
  SET bindir=%WEBROOT_PATH%\bin
)

FOR %%F IN (*Job.dll) DO (
 SET jobname=%%F
 goto done
)
:done

robocopy %bindir% .\ "Exceptionless.Core*" "Exceptionless.Insulation*" /NFL /NDL /NJH /NJS /nc /ns /np
robocopy %bindir% bin\ /S /NFL /NDL /NJH /NJS /nc /ns /np
IF %ERRORLEVEL% GEQ 8 exit 1

dotnet %jobname% %*