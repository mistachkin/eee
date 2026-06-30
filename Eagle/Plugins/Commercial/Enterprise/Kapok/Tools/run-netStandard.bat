@ECHO OFF

::
:: run-netStandard.bat --
::
:: Extensible Adaptable Generalized Logic Engine (Eagle)
:: Kapok Server Runner for the .NET Standard
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
REM       the .NET Core Host via the command line; therefore, normalize it.
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

IF NOT DEFINED DOTNET (
  SET DOTNET=dotnet.exe
)

%_VECHO% DotNet = '%DOTNET%'

FOR %%T IN (%DOTNET%) DO (
  SET %%T_PATH=%%~dp$PATH:T
)

%_VECHO% DotNetExePath = '%dotnet.exe_PATH%'

IF NOT DEFINED %DOTNET%_PATH (
  ECHO The executable "%DOTNET%" is required to be in the PATH.
  GOTO errors
)

IF NOT DEFINED EXEC_SUBCOMMANDS (
  SET EXEC_SUBCOMMANDS=exec
)

%_VECHO% ExecSubcommands = '%EXEC_SUBCOMMANDS%'

IF DEFINED ProgramFiles(x86) GOTO pfiles_x64
:pfiles_x86
SET PFILES=%ProgramFiles%
GOTO pfiles_end
:pfiles_x64
SET PFILES=%ProgramFiles(x86)%
GOTO pfiles_end
:pfiles_end

IF NOT DEFINED NOKAPOKDATAFROMPACKAGE (
  SET KapokDataFromPackage=1
)

IF NOT DEFINED NOASPNETCOREURLS (
  IF NOT DEFINED ASPNETCORE_URLS (
    IF DEFINED ASPNETCORE_DEVELOPMENT_URLS (
      SET ASPNETCORE_URLS=http://localhost:1195/
    ) ELSE (
      SET ASPNETCORE_URLS=http://localhost:11452/
    )
  )
)

%_VECHO% PFiles = '%PFILES%'
%_VECHO% KapokDataFromPackage = '%KapokDataFromPackage%'
%_VECHO% AspNetCoreUrls = '%ASPNETCORE_URLS%'

%_AECHO% Attempting to run Kapok via .NET Core Host...

%_CECHO% "%DOTNET%" %EXEC_SUBCOMMANDS% "%KAPOK%\bin\Kapok.dll"
%__ECHO% "%DOTNET%" %EXEC_SUBCOMMANDS% "%KAPOK%\bin\Kapok.dll"

IF ERRORLEVEL 1 (
  ECHO Failed to run Kapok via .NET Core Host.
  GOTO errors
)

GOTO no_errors

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
