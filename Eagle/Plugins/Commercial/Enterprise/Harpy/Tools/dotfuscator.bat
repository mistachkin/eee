@ECHO OFF

::
:: dotfuscator.bat --
::
:: Extensible Adaptable Generalized Logic Engine (Eagle)
:: Dotfuscator Community Edition Wrapper Tool
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

SET TOOLS=%~dp0
SET TOOLS=%TOOLS:~0,-1%

%_VECHO% Tools = '%TOOLS%'

SET ROOT=%~dp0\..\..\..\..\..
SET ROOT=%ROOT:\\=\%

%_VECHO% Root = '%ROOT%'

SET CONFIGURATION=%1

IF DEFINED CONFIGURATION (
  CALL :fn_UnquoteVariable CONFIGURATION
) ELSE (
  %_AECHO% No configuration specified, using default...
  SET CONFIGURATION=Release
)

%_VECHO% Configuration = '%CONFIGURATION%'

SET PROJECTFILE=%2

IF DEFINED PROJECTFILE (
  CALL :fn_UnquoteVariable PROJECTFILE
) ELSE (
  %_AECHO% No project file specified, using default...
  SET PROJECTFILE=%TOOLS%\data\Harpy.dxml
)

SET PROJECTFILE=%PROJECTFILE:\\=\%

%_VECHO% ProjectFile = '%PROJECTFILE%'

SET STRICT=%3

IF DEFINED STRICT (
  CALL :fn_UnquoteVariable STRICT
) ELSE (
  %_AECHO% Strict mode not specified, using default...
)

%_VECHO% Strict = '%STRICT%'

IF NOT DEFINED MILLISECONDS (
  SET MILLISECONDS=60000
)

%_VECHO% Milliseconds = '%MILLISECONDS%'

IF NOT DEFINED SAVE (
  SET SAVE=false
)

%_VECHO% Save = '%SAVE%'

SET EAGLEBINDIR=%ROOT%\bin\%CONFIGURATION%\bin
SET EAGLEBINDIR=%EAGLEBINDIR:\\=\%

%_VECHO% EagleBinDir = '%EAGLEBINDIR%'

SET PATH=%EAGLEBINDIR%;%PATH%

%_VECHO% Path = '%PATH%'

REM
REM NOTE: The reference directory value must have the trailing backslash.
REM       Also, in order to avoid a single trailing backslash from messing
REM       up the resulting registry command, it must be doubled.
REM
SET EAGLEREFDIR=%EAGLEBINDIR%\\

REM
REM NOTE: If the Eagle core library is found, there is nothing else to do.
REM
IF EXIST "%EAGLEREFDIR%Eagle.dll" (
  GOTO skip_referenceCheck
)

REM
REM NOTE: If the Eagle core library assembly is not found within the binary
REM       directory, attempt to use the last known good build unless we are
REM       forbidden from doing so.
REM
IF DEFINED NOLKG GOTO skip_referenceCheck

IF NOT DEFINED LKG (
  ECHO The LKG environment variable must be set first.
  GOTO errors
)

REM
REM NOTE: The reference directory value must have the trailing backslash.
REM       Also, in order to avoid a single trailing backslash from messing
REM       up the resulting registry command, it must be doubled.
REM
SET EAGLEREFDIR=%LKG%\Eagle\bin\\

:skip_referenceCheck

%_VECHO% EagleRefDir = '%EAGLEREFDIR%'

REM
REM NOTE: If the Eagle binaries are already present in the build directory for
REM       the specified configuration, just use them instead of trying to find
REM       the last known good build.
REM
IF EXIST "%EAGLEBINDIR%\EagleShell.exe" (
  GOTO skip_lastKnownGood
)

REM
REM NOTE: Attempt to use the last known good build of Eagle to set the source
REM       identifier for the current build.  If the NOLKG environment variable
REM       is defined, we assume that the last known good build of Eagle will
REM       already be in the path; otherwise, the build will fail ^(below^) when
REM       we try to update the source identifier.
REM
IF DEFINED NOLKG GOTO skip_lastKnownGood

IF NOT DEFINED LKG (
  ECHO The LKG environment variable must be set first.
  GOTO errors
)

SET SRCLKGBINDIR=%LKG%\Eagle\bin
SET SRCLKGBINDIR=%SRCLKGBINDIR:\\=\%

%_VECHO% SrcLkgBinDir = '%SRCLKGBINDIR%'

IF NOT EXIST "%SRCLKGBINDIR%\EagleShell.exe" (
  ECHO The file "%SRCLKGBINDIR%\EagleShell.exe" does not exist.
  GOTO errors
)

SET PATH=%PATH%;%SRCLKGBINDIR%

%_VECHO% Path = '%PATH%'

:skip_lastKnownGood

REM
REM HACK: Must escape parenthesis contained with the external environment
REM       variables that we plan to use within IF/ELSE blocks.
REM
IF NOT DEFINED VS80COMNTOOLS GOTO skip_vs80
SET VS80COMNTOOLS=%VS80COMNTOOLS:(=^^^^^(%
SET VS80COMNTOOLS=%VS80COMNTOOLS:)=^^^)%
:skip_vs80

IF NOT DEFINED VS90COMNTOOLS GOTO skip_vs90
SET VS90COMNTOOLS=%VS90COMNTOOLS:(=^^^^^(%
SET VS90COMNTOOLS=%VS90COMNTOOLS:)=^^^)%
:skip_vs90

IF NOT DEFINED VS100COMNTOOLS GOTO skip_vs100
SET VS100COMNTOOLS=%VS100COMNTOOLS:(=^^^^^(%
SET VS100COMNTOOLS=%VS100COMNTOOLS:)=^^^)%
:skip_vs100

IF NOT DEFINED VS110COMNTOOLS GOTO skip_vs110
SET VS110COMNTOOLS=%VS110COMNTOOLS:(=^^^^^(%
SET VS110COMNTOOLS=%VS110COMNTOOLS:)=^^^)%
:skip_vs110

IF NOT DEFINED VS120COMNTOOLS GOTO skip_vs120
SET VS120COMNTOOLS=%VS120COMNTOOLS:(=^^^^^(%
SET VS120COMNTOOLS=%VS120COMNTOOLS:)=^^^)%
:skip_vs120

IF NOT DEFINED VS140COMNTOOLS GOTO skip_vs140
SET VS140COMNTOOLS=%VS140COMNTOOLS:(=^^^^^(%
SET VS140COMNTOOLS=%VS140COMNTOOLS:)=^^^)%
:skip_vs140

IF DEFINED NOT_DEFINED (
  %_VECHO% Vs80ComnTools = '%VS80COMNTOOLS%'
  %_VECHO% Vs90ComnTools = '%VS90COMNTOOLS%'
)

%_VECHO% Vs100ComnTools = '%VS100COMNTOOLS%'
%_VECHO% Vs110ComnTools = '%VS110COMNTOOLS%'
%_VECHO% Vs120ComnTools = '%VS120COMNTOOLS%'
%_VECHO% Vs140ComnTools = '%VS140COMNTOOLS%'

IF DEFINED NOT_DEFINED (
  IF DEFINED VS80COMNTOOLS (
    SET DOTFUSCATOR=%VS80COMNTOOLS%\..\..\Application\PreEmptive Solutions\Dotfuscator Community Edition\dotfuscator.exe
    SET DEVENV=%VS80COMNTOOLS%\..\IDE\devenv.exe
  )

  IF DEFINED VS90COMNTOOLS (
    SET DOTFUSCATOR=%VS90COMNTOOLS%\..\..\Application\PreEmptive Solutions\Dotfuscator Community Edition\dotfuscator.exe
    SET DEVENV=%VS90COMNTOOLS%\..\IDE\devenv.exe
  )
)

IF DEFINED VS100COMNTOOLS (
  SET DOTFUSCATOR=%VS100COMNTOOLS%\..\..\PreEmptive Solutions\Dotfuscator Community Edition\dotfuscator.exe
  SET DEVENV=%VS100COMNTOOLS%\..\IDE\devenv.exe
)

IF DEFINED VS110COMNTOOLS (
  REM
  REM NOTE: This is most likely broken in the default install.  See the forum
  REM       post "https://www.preemptive.com/forum/topic?f=18&t=23523" for more
  REM       details.  Basically, the entire registry sub-tree from:
  REM
  REM       HKEY_LOCAL_MACHINE\Software\Microsoft\Windows Kits
  REM
  REM       Must be exported and then imported verbatim into:
  REM
  REM       HKEY_LOCAL_MACHINE\Software\Wow6432Node\Microsoft\Windows Kits
  REM
  REM       This has been confirmed to apply to 32-bit Windows 7 and may apply
  REM       to 64-bit versions of Windows as well.
  REM
  SET DOTFUSCATOR=%VS110COMNTOOLS%\..\..\PreEmptive Solutions\Dotfuscator and Analytics Community Edition\dotfuscator.exe
  SET DEVENV=%VS110COMNTOOLS%\..\IDE\devenv.exe

  REM
  REM NOTE: Since the Dotfuscator project file is not for this version of the
  REM       product, it will show up as "unsaved" in the user interface due to
  REM       an automatic "conversion" being performed on it.  Therefore, reset
  REM       the save flag to true to prevent the remainder of the automation
  REM       from being blocked by interactive "Save changes?" dialog boxes.
  REM
  SET SAVE=true
)

IF DEFINED VS120COMNTOOLS (
  SET DOTFUSCATOR=%VS120COMNTOOLS%\..\..\PreEmptive Solutions\Dotfuscator and Analytics Community Edition\dotfuscator.exe
  SET DEVENV=%VS120COMNTOOLS%\..\IDE\devenv.exe
  SET SAVE=true
)

IF DEFINED VS140COMNTOOLS (
  SET DOTFUSCATOR=%VS140COMNTOOLS%\..\..\PreEmptive Solutions\Dotfuscator and Analytics Community Edition\dotfuscator.exe
  SET DEVENV=%VS140COMNTOOLS%\..\IDE\devenv.exe
  SET SAVE=true
)

IF NOT DEFINED DOTFUSCATOR GOTO skip_setDotfuscator
SET DOTFUSCATOR=%DOTFUSCATOR:\\=\%
:skip_setDotfuscator

IF NOT DEFINED DEVENV GOTO skip_setDevEnv
SET DEVENV=%DEVENV:\\=\%
:skip_setDevEnv

%_VECHO% Dotfuscator = '%DOTFUSCATOR%'
%_VECHO% DevEnv = '%DEVENV%'

IF NOT DEFINED DOTFUSCATOR (
  REM
  REM NOTE: If this variable is not set then we must have failed to find any of
  REM       the Visual Studio environment variables we needed; hence, Visual
  REM       Studio may not be properly installed.
  REM
  ECHO Supported versions of Visual Studio do not appear to be installed.

  REM
  REM NOTE: In strict mode, fail now.  Otherwise, simply skip attempting to
  REM       obfuscate the assembly because we are probably not running on a
  REM       machine with Visual Studio installed.
  REM
  IF DEFINED STRICT (
    GOTO errors
  ) ELSE (
    ECHO Skipping obfuscation of assembly...
    GOTO no_errors
  )
)

IF NOT EXIST "%DOTFUSCATOR%" (
  ECHO Dotfuscator executable file does not exist.

  REM
  REM NOTE: In strict mode, fail now.  Otherwise, simply skip attempting to
  REM       obfuscate the assembly because we are probably not running on a
  REM       machine with Dotfuscator installed.
  REM
  IF DEFINED STRICT (
    GOTO errors
  ) ELSE (
    ECHO Skipping obfuscation of assembly...
    GOTO no_errors
  )
)

CALL :fn_ResetErrorLevel

REM
REM HACK: Temporarily add an AssemblyFolders registry sub-key to allow the
REM       Dotfuscator Community Edition to find the Eagle core library
REM       assembly.
REM
%__ECHO% reg.exe ADD HKLM\Software\Microsoft\.NETFramework\AssemblyFolders\EagleDotfuscator /ve /t REG_EXPAND_SZ /d "%EAGLEREFDIR%" /f > NUL 2>&1

REM
REM BUGBUG: Apparently, "reg.exe" does not always set the ERRORLEVEL correctly
REM         if/when it fails.  However, the worst that will happen in this case
REM         is that Dotfuscator will fail to find the Eagle core library
REM         assembly.
REM
IF %ERRORLEVEL% NEQ 0 (
  ECHO Failed to add temporary AssemblyFolders registry sub-key.

  REM
  REM NOTE: In strict mode, fail now.  Otherwise, simply skip attempting to
  REM       obfuscate the assembly because we are probably not running with
  REM       elevated administrator privileges; therefore, we cannot add the
  REM       necessary registry key.
  REM
  IF DEFINED STRICT (
    GOTO errors
  ) ELSE (
    ECHO Skipping obfuscation of assembly...
    GOTO no_errors
  )
) ELSE (
  ECHO Added temporary AssemblyFolders registry sub-key with value "%EAGLEREFDIR%".
)

REM
REM NOTE: Attempt to automate Dotfuscator synchronously, waiting for it to
REM       finish its work and exit prior to deleting the temporarily added
REM       AssemblyFolders registry sub-key.  The surrounding double quotes
REM       are certainly necessary here because the path will contain quite
REM       a few spaces.
REM
%__ECHO% EagleShell.exe -file "%TOOLS%\dotfuscator.eagle" "%DEVENV%" "%DOTFUSCATOR%" "%CONFIGURATION%" "%PROJECTFILE%" %MILLISECONDS% %SAVE%

IF ERRORLEVEL 1 (
  ECHO Automation of Dotfuscator user interface failed.
  CALL :fn_Cleanup
  GOTO errors
)

CALL :fn_Cleanup
GOTO no_errors

:fn_Cleanup
  REM
  REM HACK: Remove the temporarily added AssemblyFolders registry sub-key.
  REM
  %__ECHO% reg.exe DELETE HKLM\Software\Microsoft\.NETFramework\AssemblyFolders\EagleDotfuscator /f > NUL 2>&1

  REM
  REM BUGBUG: Apparently, "reg.exe" does not always set the ERRORLEVEL correctly
  REM         if/when it fails.  However, the worst that will happen in this
  REM         case is that we will leave behind a superfluous AssemblyFolders
  REM         registry sub-key.
  REM
  IF %ERRORLEVEL% NEQ 0 (
    ECHO Failed to delete temporary AssemblyFolders registry sub-key.
    GOTO errors
  ) ELSE (
    ECHO Deleted temporary AssemblyFolders registry sub-key.
  )
  GOTO :EOF

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
  ECHO Usage: %~nx0 [configuration]
  ECHO.
  GOTO errors

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
