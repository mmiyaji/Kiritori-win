@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================================
REM  Kiritori �����[�X�p�b�P�[�W�쐬�i�r���h��������ZIP�j
REM  �g����:
REM    1) �_�u���N���b�N �� �o�[�W�������� �� ���� no-pause
REM    2) �R�}���h:  release.bat 1.2.1 [--pause|--no-pause] [--aio] [--portable|--portable-marker-only] [--with-ja]
REM ============================================================

REM ---- ����: ���� --pause �̂Ƃ����� pause�A���� no-pause
set "PAUSE_AT_END="

REM ---- �t���O�i����I�t�j
set "AIO="
set "PORTABLE="
set "PORTABLE_MARKER_ONLY="
set "WITH_JA="

if "%~1"=="" (
  echo �o�[�W�����ԍ�����͂��Ă��������i��: 1.2.1�j:
  set /p VERSION=Version: 
  if "!VERSION!"=="" (
    echo [ERROR] �o�[�W�������w�肳��܂���ł����B
    goto :end_fail
  )
) else (
  set "VERSION=%~1"
)

REM ---- �I�v�V���� (--pause / --no-pause �Ȃ�)
if /I "%~2"=="--pause"     set "PAUSE_AT_END=1"
if /I "%~2"=="--no-pause"  set "PAUSE_AT_END="

for %%A in (%*) do (
  if /I "%%~A"=="--pause"                 set "PAUSE_AT_END=1"
  if /I "%%~A"=="--no-pause"              set "PAUSE_AT_END="
  if /I "%%~A"=="--aio"                   set "AIO=1"
  if /I "%%~A"=="--portable"              set "PORTABLE=1"
  if /I "%%~A"=="--portable-marker-only"  set "PORTABLE_MARKER_ONLY=1"
  if /I "%%~A"=="--with-ja"               set "WITH_JA=1"
)

REM ---- �p�X
set "ROOT=%~dp0"
set "SLN=%ROOT%Kiritori.sln"

REM ---- Git �Z�k�n�b�V�� �� InformationalVersion �ɕt�^�i�C�Ӂj
@REM for /f %%G in ('git rev-parse --short HEAD 2^>nul') do set "GITHASH=%%G"
if defined GITHASH (
  set "INFOVER=%VERSION%+g%GITHASH%"
) else (
  set "INFOVER=%VERSION%"
)

echo [INFO] Version        : %VERSION%  (IV=%INFOVER%)
echo [INFO] Solution       : %SLN%
echo [INFO] Flags          : AIO=%AIO%  PORTABLE=%PORTABLE%  PORTABLE_MARKER_ONLY=%PORTABLE_MARKER_ONLY%  WITH_JA=%WITH_JA%

REM ---- msbuild ���o�iPATH �� vswhere�j
set "MSBUILD_EXE=msbuild"
where %MSBUILD_EXE% >nul 2>&1
if errorlevel 1 (
  set "MSBUILD_EXE="
  if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "usebackq tokens=*" %%I in (`
      "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
    `) do set "MSBUILD_EXE=%%I"
  )
)
if not defined MSBUILD_EXE (
  echo [ERROR] msbuild ��������܂���BDeveloper Command Prompt �Ŏ��s���Ă��������B
  goto :end_fail
)

REM ---- Rebuild�iRelease/Any CPU�j�{ �o�[�W��������
echo [INFO] Building (Rebuild Release)...
"%MSBUILD_EXE%" "%SLN%" /t:Rebuild /v:m ^
  /p:Configuration=Release /p:Platform="Any CPU" ^
  /p:Version=%VERSION% ^
  /p:AssemblyVersion=%VERSION%.0 ^
  /p:FileVersion=%VERSION%.0 ^
  /p:InformationalVersion=%INFOVER%
if errorlevel 1 (
  echo [ERROR] �r���h�Ɏ��s���܂����B
  goto :end_fail
)

REM ---- ���ʕ��̏ꏊ�i�D��: Package �o�� �� ��: ������ bin\Release�j
set "BIN1=%ROOT%KiritoriPackage\bin\AnyCPU\Release\Kiritori"
set "BIN2=%ROOT%Kiritori\bin\Release"
set "BIN="
if exist "%BIN1%\Kiritori.exe" set "BIN=%BIN1%"
if not defined BIN if exist "%BIN2%\Kiritori.exe" set "BIN=%BIN2%"
if not defined BIN (
  echo [ERROR] ���s�t�@�C����������܂���:
  echo        ���s1: %BIN1%\Kiritori.exe
  echo        ���s2: %BIN2%\Kiritori.exe
  goto :end_fail
)

set "EXE=%BIN%\Kiritori.exe"
set "SAT_JA_DIR=%BIN%\ja"

REM ---- ThirdParty�i���|�W�g�����̃\�[�X�z�u�z��j
set "TP_SRC=%ROOT%Kiritori\ThirdParty"
set "TP_DST_REL=ThirdParty"
set "TP_DST="

set "README_SRC=%ROOT%README.md"
set "LICENSE_SRC="
for %%F in ("%ROOT%LICENSE" "%ROOT%LICENSE.txt" "%ROOT%LICENSE.md") do (
  if not defined LICENSE_SRC if exist "%%~fF" set "LICENSE_SRC=%%~fF"
)

REM ---- �o�͖����t���O�ɉ����Č���
set "OUTBASE=%ROOT%dist"
set "OUTNAME=Kiritori-%VERSION%"
if defined PORTABLE set "OUTNAME=%OUTNAME%-portable"
if defined AIO set "OUTNAME=%OUTNAME%-aio"
set "OUTDIR=%OUTBASE%\%OUTNAME%"
set "ZIP=%OUTBASE%\%OUTNAME%.zip"

echo [INFO] Build Dir     : %BIN%
echo [INFO] Out Dir       : %OUTDIR%

REM ---- dist �㏑���i�����o�[�W�����ł��K�������č�蒼���j
if not exist "%OUTBASE%" mkdir "%OUTBASE%" >nul 2>&1
if exist "%OUTDIR%" rd /s /q "%OUTDIR%" >nul 2>&1
mkdir "%OUTDIR%" || goto :end_fail
if exist "%ZIP%" del "%ZIP%" >nul 2>&1

REM ---- �z�u�iEXE / Config / DLL�j
set "EXENAME=Kiritori.exe"
copy /y "%EXE%" "%OUTDIR%\%EXENAME%" >nul || goto :end_fail

if exist "%EXE%.config" (
  copy /y "%EXE%.config" "%OUTDIR%\Kiritori.exe.config" >nul
  echo [INFO] EXE.config �𓯍����܂����B
)

@REM REM DLL�i���[�g���� *.dll �̂݁j
@REM robocopy "%BIN%" "%OUTDIR%" *.dll /R:0 /W:0 /NFL /NDL /NJH /NJS /NP >nul
@REM if errorlevel 8 (
@REM   echo [ERROR] DLL �R�s�[�Ɏ��s���܂����B
@REM   goto :end_fail
@REM ) else (
@REM   echo [INFO] DLL �𓯍����܂����i%BIN%\*.dll�j�B
@REM )

REM ---- �g���Ǘ�DLL�̖��������i�g�����烍�[�h������z��̂��́j
for %%F in (
  CommunityToolkit.WinUI.Notifications.dll
) do (
  if exist "%OUTDIR%\%%~F" del /f /q "%OUTDIR%\%%~F" >nul
)

REM ---- ja �T�e���C�g�i�K�v���̂݁j
if defined WITH_JA (
  if exist "%SAT_JA_DIR%\" (
    mkdir "%OUTDIR%\ja" >nul 2>&1
    xcopy /y /q "%SAT_JA_DIR%\*.resources.dll" "%OUTDIR%\ja\" >nul
    echo [INFO] ja �T�e���C�g DLL �𓯍����܂����B
  ) else (
    echo [INFO] ja �T�e���C�g DLL �͌�����܂���ł����i�X�L�b�v�j�B
  )
) else (
  echo [INFO] ja �T�e���C�g�͊g���Œ񋟁i�������܂���j�B
)

REM ---- ThirdPartyManifests�i�O��JSON�𓯍��F������Ζ��ߍ��݂ŋN���j
if exist "%BIN%\ThirdPartyManifests\" (
  robocopy "%BIN%\ThirdPartyManifests" "%OUTDIR%\ThirdPartyManifests" *.json /R:0 /W:0 /NFL /NDL /NJH /NJS /NP >nul
  echo [INFO] ThirdPartyManifests �𓯍����܂����i�O��JSON�^���ߍ��݂ǂ���ł��N���j�B
) else (
  echo [INFO] ThirdPartyManifests �͌�����܂���i���ߍ��݂Ƀt�H�[���o�b�N�j�B
)

REM -----------------------------------------------------------------
REM  ThirdParty �����̂܂ܓ����iffmpeg.exe ���j�c AIO �̂Ƃ�����
REM -----------------------------------------------------------------
if defined AIO (
  set "TP_DST=%OUTDIR%\%TP_DST_REL%"
  if exist "%TP_SRC%\" (
    robocopy "%TP_SRC%" "%TP_DST%" *.* /E /R:0 /W:0 /NFL /NDL /NJH /NJS /NP >nul
    if errorlevel 8 (
      echo [ERROR] ThirdParty �R�s�[�Ɏ��s���܂����B
      goto :end_fail
    ) else (
      echo [INFO] ThirdParty �𓯍����܂����i%TP_DST_REL%\ �z���j�B
    )
  ) else (
    echo [WARN ] ThirdParty �t�H���_��������܂���ł����i�����X�L�b�v�j�B
  )
) else (
  echo [INFO] AIO �ł͂Ȃ����� ThirdParty �͓������܂���B
)

REM ---- LICENSE/README �R�s�[
set "OUT_LICENSE=%OUTDIR%\LICENSE.txt"
if defined LICENSE_SRC (
  copy /y "%LICENSE_SRC%" "%OUT_LICENSE%" >nul
)
if exist "%README_SRC%" (
  copy /y "%README_SRC%" "%OUTDIR%\README.md" >nul
)

REM ---- AIO �̏ꍇ�� ThirdParty\ffmpeg �� LICENSE ���Y�t�i����΁j
if defined AIO (
  if exist "%TP_SRC%\ffmpeg\LICENSE.txt" (
    mkdir "%TP_DST%\ffmpeg" >nul 2>&1
    copy /y "%TP_SRC%\ffmpeg\LICENSE.txt" "%TP_DST%\ffmpeg\LICENSE.txt" >nul
  )
  for %%C in ("%TP_SRC%\ffmpeg\COPYING"* "%TP_SRC%\ffmpeg\COPYRIGHT"* "%TP_SRC%\ffmpeg\README"* ) do (
    if exist "%%~fC" (
      mkdir "%TP_DST%\ffmpeg" >nul 2>&1
      copy /y "%%~fC" "%TP_DST%\ffmpeg\" >nul
    )
  )
)

REM ---- �|�[�^�u���}�[�J�[��EXE���ύX
if defined PORTABLE_MARKER_ONLY (
  break > "%OUTDIR%\Kiritori.portable"
)
if defined PORTABLE (
  break > "%OUTDIR%\Kiritori.portable"
  ren "%OUTDIR%\%EXENAME%" "Kiritori-portable.exe"
  set "EXENAME=Kiritori-portable.exe"
)

REM ---- ���ߍ��܂ꂽ�o�[�W�����̊m�F�i���O�o�͗p�j
powershell -NoProfile -Command ^
  "$v=[System.Diagnostics.FileVersionInfo]::GetVersionInfo('%OUTDIR%\%EXENAME%');" ^
  "Write-Host ('[INFO] FileVersion=' + $v.FileVersion + '  ProductVersion=' + $v.ProductVersion)"

REM ---- SHA-256�iEXE���ɒǏ]�j
echo [INFO] ����: %EXENAME%.sha256
powershell -NoProfile -Command "$h=(Get-FileHash -Algorithm SHA256 -Path '%OUTDIR%\%EXENAME%').Hash; $line=$h + ' *%EXENAME%'; Set-Content -Path '%OUTDIR%\%EXENAME%.sha256' -Value $line -Encoding ASCII; Write-Host ('SHA256=' + $h)"
if errorlevel 1 (
  echo [WARN ] PowerShell ���s�Bcertutil �������܂��c
  for /f "usebackq tokens=1" %%A in (`certutil -hashfile "%OUTDIR%\%EXENAME%" SHA256 ^| findstr /r /i "^[0-9A-F][0-9A-F]*$"`) do set "HASH=%%A"
  if not defined HASH (
    echo [ERROR] �n�b�V�������Ɏ��s���܂����B
    goto :end_fail
  )
  > "%OUTDIR%\%EXENAME%.sha256" echo %HASH% *%EXENAME%
  echo [INFO] SHA256=%HASH%
)

REM ---- ZIP�i�t�H���_���ƁB����������Ώ㏑���j
echo [INFO] ZIP �쐬: %ZIP%
powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 (
  echo [WARN ] PowerShell �̈��k�Ɏ��s�Btar �������܂��c
  tar.exe -a -c -f "%ZIP%" -C "%OUTBASE%" "%OUTNAME%"
  if errorlevel 1 goto :end_fail
)

echo.
echo [DONE] �o��:
echo   %OUTDIR%
echo   %ZIP%
goto :end_ok

:end_fail
echo.
echo [FAIL] �r���ŃG���[���������܂����B
set "RC=1"
goto :end

:end_ok
set "RC=0"

:end
if defined PAUSE_AT_END (
  echo.
  pause
)
exit /b %RC%
