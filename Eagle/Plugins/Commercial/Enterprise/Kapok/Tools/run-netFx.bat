@ECHO OFF

::
:: run-netFx.bat --
::
:: Extensible Adaptable Generalized Logic Engine (Eagle)
:: Kapok Server Runner for the .NET Framework
::
:: Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
::
:: See the file "license.terms" for information on usage and redistribution of
:: this file, and for a DISCLAIMER OF ALL WARRANTIES.
::
:: RCS: @(#) $Id: $
::

SETLOCAL

REM SET __ECHO=ECHO
REM SET __ECHO2=ECHO
REM SET __ECHO3=ECHO
IF NOT DEFINED _AECHO (SET _AECHO=REM)
IF NOT DEFINED _CECHO (SET _CECHO=REM)
IF NOT DEFINED _CECHO2 (SET _CECHO2=REM)
IF NOT DEFINED _CECHO3 (SET _CECHO3=REM)
IF NOT DEFINED _VECHO (SET _VECHO=REM)

IF DEFINED KAPOK GOTO skip_Kapok

SET KAPOK=%~dp0\..
SET KAPOK=%KAPOK:\\=\%

REM
REM HACK: The path may not contain any "." or ".." segments when passed to
REM       the ASP.NET Development Server via the command line; therefore,
REM       normalize it.
REM
FOR /F "delims=" %%D IN ('ECHO "%KAPOK%\." 2^> NUL') DO (
  SET KAPOK=%%~dpD
)

SET KAPOK=%KAPOK:~0,-1%

:skip_Kapok

%_VECHO% Kapok = '%KAPOK%'

IF NOT DEFINED KAPOK (
  ECHO The KAPOK environment variable must be set first.
  GOTO errors
)

IF NOT EXIST "%KAPOK%\bin\Kapok.dll" (
  ECHO The Kapok binary "%KAPOK%\bin\Kapok.dll" is missing.
  GOTO errors
)

IF NOT DEFINED NOEAGLE (
  IF DEFINED EAGLE (
    %_AECHO% WARNING: Unsetting the EAGLE environment variable...
    CALL :fn_UnsetVariable EAGLE
  )
)

%_VECHO% Eagle = '%EAGLE%'

IF DEFINED ProgramFiles(x86) GOTO pfiles_x64
:pfiles_x86
SET PFILES=%ProgramFiles%
GOTO pfiles_end
:pfiles_x64
SET PFILES=%ProgramFiles(x86)%
GOTO pfiles_end
:pfiles_end

IF NOT DEFINED KAPOK_IIS_EXPRESS_PORT (
  IF DEFINED KAPOK_IIS_DEVELOPMENT_PORT (
    SET KAPOK_IIS_EXPRESS_PORT=1195
  ) ELSE (
    SET KAPOK_IIS_EXPRESS_PORT=11452
  )
)

IF NOT DEFINED KAPOK_WEBDEV_WEBSERVER_PORT (
  SET KAPOK_WEBDEV_WEBSERVER_PORT=1195
)

IF NOT DEFINED NOKAPOKDATAFROMPACKAGE (
  SET KapokDataFromPackage=1
)

%_VECHO% PFiles = '%PFILES%'
%_VECHO% KapokIisExpressPort = '%KAPOK_IIS_EXPRESS_PORT%'
%_VECHO% KapokWebDevWebServerPort = '%KAPOK_WEBDEV_WEBSERVER_PORT%'
%_VECHO% KapokDataFromPackage = '%KapokDataFromPackage%'
%_VECHO% NoIisExpress = '%NO_IIS_EXPRESS%'
%_VECHO% NoWebDevWebServer = '%NO_WEBDEV_WEBSERVER%'

IF NOT DEFINED NO_IIS_EXPRESS (
  IF EXIST "%PFILES%\IIS Express\iisexpress.exe" (
    %_AECHO% Attempting to run Kapok via IIS Express...

    %_CECHO% "%PFILES%\IIS Express\iisexpress.exe" "/port:%KAPOK_IIS_EXPRESS_PORT%" "/path:%KAPOK%"
    %__ECHO% "%PFILES%\IIS Express\iisexpress.exe" "/port:%KAPOK_IIS_EXPRESS_PORT%" "/path:%KAPOK%"

    IF ERRORLEVEL 1 (
      ECHO Failed to run Kapok via IIS Express.
      GOTO errors
    )

    GOTO no_errors
  )
)

IF NOT DEFINED NO_WEBDEV_WEBSERVER (
  IF EXIST "%PFILES%\Common Files\Microsoft Shared\DevServer\9.0\WebDev.WebServer.exe" (
    %_AECHO% Attempting to run Kapok via ASP.NET Development Server...

    %_CECHO% "%PFILES%\Common Files\Microsoft Shared\DevServer\9.0\WebDev.WebServer.exe" "/port:%KAPOK_WEBDEV_WEBSERVER_PORT%" "/path:%KAPOK%"
    %__ECHO% "%PFILES%\Common Files\Microsoft Shared\DevServer\9.0\WebDev.WebServer.exe" "/port:%KAPOK_WEBDEV_WEBSERVER_PORT%" "/path:%KAPOK%"

    IF ERRORLEVEL 1 (
      ECHO Failed to run Kapok via ASP.NET Development Server.
      GOTO errors
    )

    GOTO no_errors
  )
)

ECHO No supported web server was found.
GOTO errors

:fn_UnsetVariable
  SETLOCAL
  SET VALUE=%1
  IF DEFINED VALUE (
    SET VALUE=
    ENDLOCAL
    SET %VALUE%=
  ) ELSE (
    ENDLOCAL
  )
  CALL :fn_ResetErrorLevel
  GOTO :EOF

:fn_ResetErrorLevel
  VERIFY > NUL
  GOTO :EOF

:fn_SetErrorLevel
  VERIFY MAYBE 2> NUL
  GOTO :EOF

:errors
  CALL :fn_SetErrorLevel
  ENDLOCAL
  ECHO.
  ECHO Failure, errors were encountered.
  GOTO end_of_file

:no_errors
  CALL :fn_ResetErrorLevel
  ENDLOCAL
  ECHO.
  ECHO Success, no errors were encountered.
  GOTO end_of_file

:end_of_file
%__ECHO% EXIT /B %ERRORLEVEL%
