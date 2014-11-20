@Echo OFF
SETLOCAL
SET ERRORLEVEL=

REM <dev>
@Echo ON
REM </dev>

SET ARGS=%*
SET ARGS_NO_QUOTE=%ARGS:"=%
IF NOT "%ARGS_NO_QUOTE%"=="" SET ARGS=%ARGS:/?="/?"%

CALL "%~dp0KLR.cmd" --lib "%~dp0;%~dp0lib\Microsoft.Framework.PackageManager;%~dp0lib\Microsoft.Framework.Project" "Microsoft.Framework.PackageManager" --tools-path "%~dp0lib\Microsoft.Framework.PackageManager" %ARGS%

exit /b %ERRORLEVEL%
ENDLOCAL