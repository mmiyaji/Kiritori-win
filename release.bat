@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================================
REM  Kiritori �����[�X�p�b�P�[�W�쐬�i�r���h��������ZIP�j
REM  �g����:
REM    1) �_�u���N���b�N �� �o�[�W�������� �� ���� no-pause
REM    2) �R�}���h:  release.bat 1.2.1 [--pause|--no-pause]
REM ============================================================

REM ---- ����: ���� --pause �̂Ƃ����� pause�A���� no-pause
set "PAUSE_AT_END="

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

REM ---- �I�v�V���� (--pause / --no-pause)
if /I "%~2"=="--pause"     set "PAUSE_AT_END=1"
if /I "%~2"=="--no-pause"  set "PAUSE_AT_END="

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

REM ---- Rebuild�iRelease/Any CPU�j�{ �o�[�W���������i�������́I�j
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

set "README=%ROOT%README.md"
set "OUTBASE=%ROOT%dist"
set "OUTDIR=%OUTBASE%\Kiritori-%VERSION%"
set "ZIP=%OUTBASE%\Kiritori-%VERSION%.zip"

echo [INFO] Build Dir     : %BIN%
echo [INFO] Out Dir       : %OUTDIR%

REM ---- dist �㏑���i�����o�[�W�����ł��K�������č�蒼���j
if not exist "%OUTBASE%" mkdir "%OUTBASE%" >nul 2>&1
if exist "%OUTDIR%" rd /s /q "%OUTDIR%" >nul 2>&1
mkdir "%OUTDIR%" || goto :end_fail
if exist "%ZIP%" del "%ZIP%" >nul 2>&1

REM ---- �z�u
copy /y "%EXE%" "%OUTDIR%\Kiritori.exe" >nul || goto :end_fail

if exist "%EXE%.config" (
  copy /y "%EXE%.config" "%OUTDIR%\Kiritori.exe.config" >nul
  echo [INFO] EXE.config �𓯍����܂����B
)

robocopy "%BIN%" "%OUTDIR%" *.dll /R:0 /W:0 /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  echo [ERROR] DLL �R�s�[�Ɏ��s���܂����B
  goto :end_fail
) else (
  echo [INFO] DLL �𓯍����܂����i%BIN%\*.dll�j�B
)

if exist "%README%" (
  copy /y "%README%" "%OUTDIR%\README.md" >nul
) else (
  echo [WARN ] README.md ��������܂���ł����i�����X�L�b�v�j�B
)

if exist "%SAT_JA_DIR%\" (
  mkdir "%OUTDIR%\ja" >nul 2>&1
  xcopy /y /q "%SAT_JA_DIR%\*.resources.dll" "%OUTDIR%\ja\" >nul
  echo [INFO] ja �T�e���C�g DLL �𓯍����܂����B
) else (
  echo [INFO] ja �T�e���C�g DLL �͌�����܂���ł����i�X�L�b�v�j�B
)

REM ---- ���ߍ��܂ꂽ�o�[�W�����̊m�F�i���O�o�͗p�j
powershell -NoProfile -Command ^
  "$v=[System.Diagnostics.FileVersionInfo]::GetVersionInfo('%OUTDIR%\Kiritori.exe');" ^
  "Write-Host ('[INFO] FileVersion=' + $v.FileVersion + '  ProductVersion=' + $v.ProductVersion)"

REM ---- SHA-256
echo [INFO] ����: Kiritori.exe.sha256
powershell -NoProfile -Command "$h=(Get-FileHash -Algorithm SHA256 -Path '%OUTDIR%\Kiritori.exe').Hash; $line=$h + ' *Kiritori.exe'; Set-Content -Path '%OUTDIR%\Kiritori.exe.sha256' -Value $line -Encoding ASCII; Write-Host ('SHA256=' + $h)"
if errorlevel 1 (
  echo [WARN ] PowerShell ���s�Bcertutil �������܂��c
  for /f "usebackq tokens=1" %%A in (`certutil -hashfile "%OUTDIR%\Kiritori.exe" SHA256 ^| findstr /r /i "^[0-9A-F][0-9A-F]*$"`) do set "HASH=%%A"
  if not defined HASH (
    echo [ERROR] �n�b�V�������Ɏ��s���܂����B
    goto :end_fail
  )
  > "%OUTDIR%\Kiritori.exe.sha256" echo %HASH% *Kiritori.exe
  echo [INFO] SHA256=%HASH%
)

REM ---- ZIP�i�t�H���_���ƁB����������Ώ㏑���j
echo [INFO] ZIP �쐬: %ZIP%
powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 (
  echo [WARN ] PowerShell �̈��k�Ɏ��s�Btar �������܂��c
  tar.exe -a -c -f "%ZIP%" -C "%OUTBASE%" "Kiritori-%VERSION%"
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
