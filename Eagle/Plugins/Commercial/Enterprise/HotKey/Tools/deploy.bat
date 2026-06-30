@ECHO OFF

::
:: deploy.bat --
::
:: Extensible Adaptable Generalized Logic Engine (Eagle)
:: Deployment Tool
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

%_AECHO% Running %0 %*

REM SET DFLAGS=/L

%_VECHO% DFlags = '%DFLAGS%'

SET FLAGS=/V /F /G /H /I /R /S /Y /Z

%_VECHO% Flags = '%FLAGS%'

SET FFLAGS=/V /F /G /H /I /R /Y /Z

%_VECHO% FFlags = '%FFLAGS%'

SET CONFIGURATION=%2

IF DEFINED CONFIGURATION (
  CALL :fn_UnquoteVariable CONFIGURATION
) ELSE (
  %_AECHO% No configuration specified, using default...
  SET CONFIGURATION=Release
)

%_VECHO% Configuration = '%CONFIGURATION%'

SET TARGET=%1

IF NOT DEFINED TARGET (
  GOTO usage
)

CALL :fn_UnquoteVariable TARGET

SET SOURCE=%3

IF NOT DEFINED SOURCE (
  SET SOURCE=%EAGLE%
)

CALL :fn_UnquoteVariable SOURCE

%_VECHO% Source = '%SOURCE%'
%_VECHO% Target = '%TARGET%'

SET DUMMY2=%4

IF DEFINED DUMMY2 (
  GOTO usage
)

IF NOT EXIST "%SOURCE%" (
  ECHO Cannot copy from "%SOURCE%", it does not exist.
  GOTO errors
)

IF NOT EXIST "%SOURCE%\Plugins\Commercial\Enterprise\HotKey\Scripts" (
  ECHO Cannot copy from "%SOURCE%\Plugins\Commercial\Enterprise\HotKey\Scripts", it does not exist.
  GOTO errors
)

IF NOT EXIST "%SOURCE%\Plugins\Commercial\Enterprise\HotKey\Templates" (
  ECHO Cannot copy from "%SOURCE%\Plugins\Commercial\Enterprise\HotKey\Templates", it does not exist.
  GOTO errors
)

IF NOT EXIST "%SOURCE%\bin\%CONFIGURATION%\bin" (
  ECHO Cannot copy from "%SOURCE%\bin\%CONFIGURATION%\bin", it does not exist.
  GOTO errors
)

IF NOT EXIST "%SOURCE%\bin\%CONFIGURATION%\lib" (
  ECHO Cannot copy from "%SOURCE%\bin\%CONFIGURATION%\lib", it does not exist.
  GOTO errors
)

CALL :fn_ResetErrorLevel

%__ECHO% XCOPY "%SOURCE%\Plugins\Commercial\Enterprise\HotKey\Scripts\*" "%TARGET%\HotKey1.0\Scripts\" %FFLAGS% %DFLAGS%

IF ERRORLEVEL 1 (
  ECHO Failed to copy "%SOURCE%\Plugins\Commercial\Enterprise\HotKey\Scripts\*" to "%TARGET%\HotKey1.0\Scripts\".
  GOTO errors
)

%__ECHO% XCOPY "%SOURCE%\Plugins\Commercial\Enterprise\HotKey\Templates\*" "%TARGET%\HotKey1.0\Templates\" %FFLAGS% %DFLAGS%

IF ERRORLEVEL 1 (
  ECHO Failed to copy "%SOURCE%\Plugins\Commercial\Enterprise\HotKey\Templates\*" to "%TARGET%\HotKey1.0\Templates\".
  GOTO errors
)

FOR %%F IN (Eagle.dll Eagle.pdb EagleShell.exe EagleShell.pdb) DO (
  %__ECHO% XCOPY "%SOURCE%\bin\%CONFIGURATION%\bin\%%F" "%TARGET%\" %FFLAGS% %DFLAGS%

  IF ERRORLEVEL 1 (
    ECHO Failed to copy "%SOURCE%\bin\%CONFIGURATION%\bin\%%F" to "%TARGET%\".
    GOTO errors
  )
)

FOR %%P IN (Harpy1.0 Badge1.0 HotKey1.0) DO (
  FOR %%F IN (dll pdb eagle harpy) DO (
    %__ECHO% XCOPY "%SOURCE%\bin\%CONFIGURATION%\lib\%%P\*.%%F" "%TARGET%\%%P\" %FFLAGS% %DFLAGS%

    IF ERRORLEVEL 1 (
      ECHO Failed to copy "%SOURCE%\bin\%CONFIGURATION%\lib\%%P\*.%%F" to "%TARGET%\%%P\".
      GOTO errors
    )
  )
)

GOTO no_errors

:fn_UnquoteVariable
  IF NOT DEFINED %1 GOTO :EOF
  SETLOCAL
  SET __ECHO_CMD=ECHO %%%1%%
  FOR /F "delims=" %%V IN ('%__ECHO_CMD%') DO (
    SET VALUE=%%V
  )
  SET VALUE=%VALUE:"=%
  REM "
  ENDLOCAL && SET %1=%VALUE%
  GOTO :EOF

:fn_ResetErrorLevel
  VERIFY > NUL
  GOTO :EOF

:fn_SetErrorLevel
  VERIFY MAYBE 2> NUL
  GOTO :EOF

:usage
  ECHO.
  ECHO Usage: %~nx0 ^<target^> [configuration] [source]
  GOTO errors

:errors
  CALL :fn_SetErrorLevel
  ENDLOCAL
  ECHO.
  ECHO Deploy failure, errors were encountered.
  GOTO end_of_file

:no_errors
  CALL :fn_ResetErrorLevel
  ENDLOCAL
  ECHO.
  ECHO Deploy success, no errors were encountered.
  GOTO end_of_file

:end_of_file
%__ECHO% EXIT /B %ERRORLEVEL%
