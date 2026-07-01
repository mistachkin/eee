@ECHO OFF

::
:: link.bat --
::
:: Eagle Enterprise Edition (EEE)
:: Submodule Meld Tool (Windows)
::
:: Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
::
:: See the file "license.terms" for information on usage and redistribution of
:: this file, and for a DISCLAIMER OF ALL WARRANTIES.
::
:: RCS: @(#) $Id: $
::
:: Melds the enterprise overlay (under "<eee>\Eagle") and the parent Eagle core
:: checkout together with directory junctions (for whole directories) and file
:: symbolic links (for loose files).  Linking happens in BOTH directions:
::
::   overlay -> core : everything under "<eee>\Eagle" appears at the matching
::     core-relative path, so the enterprise solutions, plugin projects, and
::     signing keys are where their project files expect them.
::
::   core -> overlay : the plugin ".csproj" files use EagleDir=<eee>\Eagle and
::     reference the core Library project and Targets as "<eee>\Eagle\Library"
::     and "<eee>\Eagle\Targets", so those names are junctioned back to the
::     core's own "Library" and "Targets" directories (see CORE_DIR_LINKS).
::
:: See "link.sh" for the POSIX equivalent.
::
:: Creating file symbolic links on Windows requires elevated administrator
:: privileges or Developer Mode; this tool refuses to run without that ability.
:: (Directory junctions do not require elevation.)
::

REM ****************************************************************************
REM ******************** Prologue / Command Line Processing ********************
REM ****************************************************************************

SETLOCAL

ECHO LINK STARTED ON %DATE% AT %TIME% BY %USERDOMAIN%\%USERNAME%

IF NOT DEFINED _AECHO (SET _AECHO=REM)
IF NOT DEFINED _VECHO (SET _VECHO=REM)

%_AECHO% Running %0 %*

SET OVERLAY_SUBDIR=Eagle
SET CORE_DIR_LINKS=Library Targets
SET ACTION=link
SET DRY_RUN=
SET CORE_OVERRIDE=

:parse_args
IF "%~1" == "" GOTO args_done
IF /I "%~1" == "--dry-run" (SET DRY_RUN=1 & SHIFT & GOTO parse_args)
IF /I "%~1" == "--unlink" (SET ACTION=unlink & SHIFT & GOTO parse_args)
IF /I "%~1" == "--core" (SET CORE_OVERRIDE=%~2 & SHIFT & SHIFT & GOTO parse_args)
IF /I "%~1" == "-h" GOTO usage
IF /I "%~1" == "--help" GOTO usage
IF /I "%~1" == "/?" GOTO usage
ECHO Unknown argument: %~1
GOTO usage
:args_done

REM ****************************************************************************
REM *********************** Resolve Overlay / Core Paths ***********************
REM ****************************************************************************

SET TOOLS=%~dp0
FOR %%I IN ("%TOOLS%..") DO SET EEE_ROOT=%%~fI
SET OVERLAY=%EEE_ROOT%\%OVERLAY_SUBDIR%

%_VECHO% EeeRoot = '%EEE_ROOT%'
%_VECHO% Overlay = '%OVERLAY%'

IF NOT EXIST "%OVERLAY%" (
  ECHO The EEE overlay directory "%OVERLAY%" does not exist.
  GOTO errors
)

REM
REM NOTE: The parent Eagle core checkout: an explicit override wins, then the
REM       EEE_CORE_ROOT environment variable, then the git superproject, then
REM       the parent directory of the eee submodule.
REM
SET CORE=
IF DEFINED CORE_OVERRIDE SET CORE=%CORE_OVERRIDE%
IF NOT DEFINED CORE IF DEFINED EEE_CORE_ROOT SET CORE=%EEE_CORE_ROOT%

IF NOT DEFINED CORE (
  FOR /F "usebackq delims=" %%R IN (`git -C "%EEE_ROOT%" rev-parse --show-superproject-working-tree 2^>NUL`) DO (
    SET CORE=%%R
  )
)

IF NOT DEFINED CORE FOR %%I IN ("%EEE_ROOT%\..") DO SET CORE=%%~fI

REM NOTE: Normalize any forward slashes (e.g. from git) and resolve to a full path.
SET CORE=%CORE:/=\%
FOR %%I IN ("%CORE%") DO SET CORE=%%~fI

%_VECHO% Core = '%CORE%'

IF NOT EXIST "%CORE%" (
  ECHO The Eagle core checkout "%CORE%" does not exist.
  GOTO usage
)

IF NOT EXIST "%CORE%\Eagle.sln" IF NOT EXIST "%CORE%\Library\" (
  ECHO "%CORE%" does not look like an Eagle core checkout ^(no Eagle.sln or Library\^).
  ECHO Pass --core ^<dir^> or set the EEE_CORE_ROOT environment variable.
  GOTO usage
)

SET CONFLICT_MARKER=%TEMP%\eee-link-conflict.%RANDOM%.tmp
DEL /F /Q "%CONFLICT_MARKER%" 2>NUL

REM ****************************************************************************
REM *********************** Symbolic Link Capability Check *********************
REM ****************************************************************************

REM
REM NOTE: Fail fast (unless we are only previewing or unlinking) if we cannot
REM       create file symbolic links here; on Windows this requires elevated
REM       administrator privileges or Developer Mode.  Directory junctions do
REM       not, but the loose overlay files do.
REM
IF /I "%ACTION%" == "unlink" GOTO skip_probe
IF DEFINED DRY_RUN GOTO skip_probe

SET PROBE_TARGET=%CORE%\.eee-probe-target.%RANDOM%
SET PROBE_LINK=%CORE%\.eee-probe-link.%RANDOM%
ECHO probe> "%PROBE_TARGET%"
mklink "%PROBE_LINK%" "%PROBE_TARGET%" >NUL 2>&1
IF ERRORLEVEL 1 GOTO no_symlink
DEL /F /Q "%PROBE_LINK%" 2>NUL
DEL /F /Q "%PROBE_TARGET%" 2>NUL

:skip_probe

REM ****************************************************************************
REM ******************************* Meld / Unmeld *****************************
REM ****************************************************************************

ECHO EEE overlay : %OVERLAY%
ECHO Eagle core  : %CORE%
IF DEFINED DRY_RUN (
  ECHO Action      : %ACTION% ^(dry-run^)
) ELSE (
  ECHO Action      : %ACTION%
)
ECHO.

REM Create the core -> overlay links first when melding (so the tree is complete
REM before the walk), and last when unmelding; the walk skips these names.
IF /I "%ACTION%" == "link" (
  FOR %%N IN (%CORE_DIR_LINKS%) DO CALL :fn_CoreLink "%%N"
  CALL :fn_Walk ""
) ELSE (
  CALL :fn_Walk ""
  FOR %%N IN (%CORE_DIR_LINKS%) DO CALL :fn_CoreLink "%%N"
)

IF EXIST "%CONFLICT_MARKER%" (
  DEL /F /Q "%CONFLICT_MARKER%" 2>NUL
  GOTO errors
)

GOTO no_errors

REM ****************************************************************************
REM ******************************* Subroutines ******************************
REM ****************************************************************************

:fn_Walk
  REM Recursively process the overlay directory whose path, relative to the
  REM overlay root, is given by %1 (empty for the overlay root itself).
  SETLOCAL
  SET REL=%~1
  IF DEFINED REL (SET HERE=%OVERLAY%\%REL%) ELSE (SET HERE=%OVERLAY%)
  IF NOT EXIST "%HERE%" (ENDLOCAL & GOTO :EOF)
  PUSHD "%HERE%"
  FOR %%F IN (*) DO (
    IF DEFINED REL (CALL :fn_Process "%REL%\%%~nxF" F) ELSE (CALL :fn_Process "%%~nxF" F)
  )
  FOR /D %%D IN (*) DO (
    IF DEFINED REL (
      CALL :fn_Process "%REL%\%%~nxD" D
    ) ELSE (
      REM At the overlay root, the core-dir links point the other way and are
      REM handled by fn_CoreLink, so skip them here.
      CALL :fn_IsCoreLink "%%~nxD"
      IF NOT DEFINED IS_CORE_LINK CALL :fn_Process "%%~nxD" D
    )
  )
  POPD
  ENDLOCAL
  GOTO :EOF

:fn_Process
  REM %1 = path relative to the overlay/core; %2 = F (file) or D (directory).
  SET RELPATH=%~1
  SET PTYPE=%~2
  IF EXIST "%CONFLICT_MARKER%" GOTO :EOF
  SET DST=%CORE%\%RELPATH%
  IF NOT EXIST "%DST%" GOTO fp_absent
  fsutil reparsepoint query "%DST%" >NUL 2>&1
  IF ERRORLEVEL 1 GOTO fp_real
  REM Existing reparse point (one of ours): re-use, or remove when unlinking.
  IF /I "%ACTION%" == "unlink" (CALL :fn_Remove "%RELPATH%" "%PTYPE%") ELSE (ECHO   ok       %RELPATH%)
  GOTO :EOF
:fp_real
  REM A real directory present in both overlay and core: descend (shared dir).
  IF /I NOT "%PTYPE%" == "D" GOTO fp_conflict
  IF NOT EXIST "%DST%\" GOTO fp_conflict
  CALL :fn_Walk "%RELPATH%"
  GOTO :EOF
:fp_conflict
  IF /I "%ACTION%" == "unlink" GOTO :EOF
  ECHO CONFLICT: "%RELPATH%" already exists in the core and is not an EEE link.
  ECHO conflict> "%CONFLICT_MARKER%"
  GOTO :EOF
:fp_absent
  IF /I "%ACTION%" == "unlink" GOTO :EOF
  CALL :fn_Make "%RELPATH%" "%PTYPE%"
  GOTO :EOF

:fn_Make
  REM Create a junction (directory) or symbolic link (file) at core\%1.
  SET RELPATH=%~1
  SET PTYPE=%~2
  SET DST=%CORE%\%RELPATH%
  SET TGT=%OVERLAY%\%RELPATH%
  IF /I "%PTYPE%" == "D" GOTO fm_dir
  REM Ensure the parent directory exists as a real directory.
  FOR %%P IN ("%DST%") DO SET DSTPARENT=%%~dpP
  IF NOT EXIST "%DSTPARENT%" IF NOT DEFINED DRY_RUN MKDIR "%DSTPARENT%"
  IF DEFINED DRY_RUN (ECHO   link     %RELPATH% & GOTO :EOF)
  mklink "%DST%" "%TGT%" >NUL 2>&1
  IF ERRORLEVEL 1 GOTO fm_fail
  ECHO   linked   %RELPATH%
  GOTO :EOF
:fm_dir
  IF DEFINED DRY_RUN (ECHO   junction %RELPATH% & GOTO :EOF)
  mklink /J "%DST%" "%TGT%" >NUL 2>&1
  IF ERRORLEVEL 1 GOTO fm_fail
  ECHO   junction %RELPATH%
  GOTO :EOF
:fm_fail
  ECHO Failed to create link for "%RELPATH%".
  ECHO conflict> "%CONFLICT_MARKER%"
  GOTO :EOF

:fn_Remove
  REM Remove a link previously created by this tool (leaves its target intact).
  SET RELPATH=%~1
  SET PTYPE=%~2
  SET DST=%CORE%\%RELPATH%
  IF DEFINED DRY_RUN (ECHO   unlink   %RELPATH% & GOTO :EOF)
  IF /I "%PTYPE%" == "D" (RMDIR "%DST%" >NUL 2>&1) ELSE (DEL /F /Q "%DST%" >NUL 2>&1)
  ECHO   removed  %RELPATH%
  GOTO :EOF

:fn_IsCoreLink
  REM Set IS_CORE_LINK=1 when %1 names one of the CORE_DIR_LINKS, else clear it.
  SETLOCAL
  SET RESULT=
  FOR %%N IN (%CORE_DIR_LINKS%) DO IF /I "%%N" == "%~1" SET RESULT=1
  ENDLOCAL & SET IS_CORE_LINK=%RESULT%
  GOTO :EOF

:fn_CoreLink
  REM Create (or, with --unlink, remove) a core-dir link: a directory junction at
  REM "<eee>\Eagle\%1" pointing back at the core's own "%1" directory.
  SET NAME=%~1
  SET LINKPATH=%OVERLAY%\%NAME%
  SET LINKTGT=%CORE%\%NAME%
  IF EXIST "%CONFLICT_MARKER%" GOTO :EOF
  IF EXIST "%LINKPATH%" GOTO fcl_exists
  REM Absent in the overlay: create it (when melding) if the core provides it.
  IF /I "%ACTION%" == "unlink" GOTO :EOF
  IF NOT EXIST "%LINKTGT%\" (
    ECHO   skip     %OVERLAY_SUBDIR%\%NAME% ^(core has no "%NAME%\"^)
    GOTO :EOF
  )
  IF DEFINED DRY_RUN (ECHO   junction %OVERLAY_SUBDIR%\%NAME% & GOTO :EOF)
  mklink /J "%LINKPATH%" "%LINKTGT%" >NUL 2>&1
  IF ERRORLEVEL 1 (
    ECHO Failed to create link for "%OVERLAY_SUBDIR%\%NAME%".
    ECHO conflict> "%CONFLICT_MARKER%"
    GOTO :EOF
  )
  ECHO   junction %OVERLAY_SUBDIR%\%NAME%
  GOTO :EOF
:fcl_exists
  REM Present already: it must be a reparse point (one of ours) to touch it.
  fsutil reparsepoint query "%LINKPATH%" >NUL 2>&1
  IF ERRORLEVEL 1 (
    IF /I "%ACTION%" == "unlink" GOTO :EOF
    ECHO CONFLICT: "%OVERLAY_SUBDIR%\%NAME%" already exists in the overlay and is not an EEE link.
    ECHO conflict> "%CONFLICT_MARKER%"
    GOTO :EOF
  )
  IF /I NOT "%ACTION%" == "unlink" (ECHO   ok       %OVERLAY_SUBDIR%\%NAME% & GOTO :EOF)
  IF DEFINED DRY_RUN (ECHO   unlink   %OVERLAY_SUBDIR%\%NAME% & GOTO :EOF)
  RMDIR "%LINKPATH%" >NUL 2>&1
  ECHO   removed  %OVERLAY_SUBDIR%\%NAME%
  GOTO :EOF

:fn_ResetErrorLevel
  VERIFY > NUL
  GOTO :EOF

:fn_SetErrorLevel
  VERIFY MAYBE 2> NUL
  GOTO :EOF

REM ****************************************************************************
REM ********************************* Epilogue ********************************
REM ****************************************************************************

:no_symlink
  DEL /F /Q "%PROBE_LINK%" 2>NUL
  DEL /F /Q "%PROBE_TARGET%" 2>NUL
  ECHO This tool requires the ability to create file symbolic links.
  ECHO Run it from an [elevated] administrator command prompt, or enable Windows
  ECHO Developer Mode, then try again.
  GOTO errors

:usage
  ECHO.
  ECHO Usage: %~nx0 [--dry-run] [--unlink] [--core ^<dir^>]
  ECHO.
  ECHO Melds the Eagle Enterprise Edition overlay ^(under "^<eee^>\%OVERLAY_SUBDIR%"^)
  ECHO and the parent Eagle core checkout together using directory junctions and
  ECHO file symbolic links ^(in both directions; see the header of this script^).
  ECHO.
  ECHO   --dry-run      show what would happen; make no changes
  ECHO   --unlink       remove the EEE links from the core ^(instead of creating^)
  ECHO   --core ^<dir^>   parent Eagle core checkout ^(default: auto-detected^)
  ECHO   -h, --help     this help
  ECHO.
  ECHO The EEE_CORE_ROOT environment variable may be used instead of --core.
  ECHO.
  ECHO Creating file symbolic links on Windows requires [elevated] administrator
  ECHO privileges or Developer Mode; this tool will refuse to run without them.
  GOTO errors

:errors
  CALL :fn_SetErrorLevel
  ENDLOCAL
  ECHO.
  ECHO Link failure, errors were encountered.
  GOTO end_of_file

:no_errors
  CALL :fn_ResetErrorLevel
  ENDLOCAL
  ECHO.
  ECHO Link success, no errors were encountered.
  GOTO end_of_file

:end_of_file
ECHO LINK STOPPED ON %DATE% AT %TIME% BY %USERDOMAIN%\%USERNAME%
EXIT /B %ERRORLEVEL%
