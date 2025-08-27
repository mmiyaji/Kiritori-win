@echo off
setlocal EnableExtensions
REM ============================================================
REM  Kiritori �����[�X�p�b�P�[�W�쐬
REM  �g����:
REM    1) �_�u���N���b�N �� �o�[�W�����𕷂���� �� �I������ pause
REM    2) �R�}���h���C��:  release.bat 1.1.3 [--pause|--no-pause]
REM ============================================================

REM ---- ����̓���: �Θb���s(�����Ȃ�)�Ȃ� pause�A��������Ȃ� no-pause
set "PAUSE_AT_END="

if "%~1"=="" (
  echo �o�[�W�����ԍ�����͂��Ă��������i��: 1.1.3�j:
  set /p VERSION=Version: 
  if "%VERSION%"=="" (
    echo [ERROR] �o�[�W�������w�肳��܂���ł����B
    set "PAUSE_AT_END=1"
    goto :end_fail
  )
  set "PAUSE_AT_END=1"
) else (
  set "VERSION=%~1"
)

REM ---- �I�v�V���� (--pause / --no-pause)
if /I "%~2"=="--pause"     set "PAUSE_AT_END=1"
if /I "%~2"=="--no-pause"  set "PAUSE_AT_END="

REM ---- �p�X
set "ROOT=%~dp0"
set "BIN=%ROOT%Kiritori\bin\Release"
set "EXE=%BIN%\Kiritori.exe"
set "EXE_CFG=%BIN%\Kiritori.exe.config"
set "SAT_JA_DIR=%BIN%\ja"

set "README=%ROOT%README.md"

set "OUTBASE=%ROOT%dist"
set "OUTDIR=%OUTBASE%\Kiritori-%VERSION%"
set "ZIP=%OUTBASE%\Kiritori-%VERSION%.zip"

echo [INFO] Version    : %VERSION%
echo [INFO] Build Dir  : %BIN%
echo [INFO] Out Dir    : %OUTDIR%

REM ---- ���͊m�F
if not exist "%EXE%" (
  echo [ERROR] ���s�t�@�C����������܂���: %EXE%
  set "PAUSE_AT_END=1"
  goto :end_fail
)

REM ---- �o�̓f�B���N�g���̗p��
if not exist "%OUTBASE%" mkdir "%OUTBASE%" >nul 2>&1
if exist "%OUTDIR%" rd /s /q "%OUTDIR%" >nul 2>&1
mkdir "%OUTDIR%" || goto :end_fail

REM ---- �t�@�C���z�u
copy /y "%EXE%" "%OUTDIR%\Kiritori.exe" >nul || goto :end_fail
if exist "%EXE_CFG%" copy /y "%EXE_CFG%" "%OUTDIR%\Kiritori.exe.config" >nul

if exist "%README%" (
  copy /y "%README%" "%OUTDIR%\README.md" >nul
) else (
  echo [WARN ] README.md ��������܂���ł����i�����X�L�b�v�j�B
)

REM ---- �T�e���C�gDLL(ja) ����
if exist "%SAT_JA_DIR%\" (
  mkdir "%OUTDIR%\ja" >nul 2>&1
  xcopy /y /q "%SAT_JA_DIR%\*.resources.dll" "%OUTDIR%\ja\" >nul
  echo [INFO] ja �T�e���C�g DLL �𓯍����܂����B
) else (
  echo [INFO] ja �T�e���C�g DLL �͌�����܂���ł����i�X�L�b�v�j�B
)

REM ---- SHA-256 �n�b�V��
echo [INFO] ����: Kiritori.exe.sha256
powershell -NoProfile -Command ^
  "$h=(Get-FileHash -Algorithm SHA256 -Path '%OUTDIR%\Kiritori.exe').Hash;" ^
  "$line=$h + ' *Kiritori.exe';" ^
  "Set-Content -Path '%OUTDIR%\Kiritori.exe.sha256' -Value $line -Encoding ASCII;" ^
  "Write-Host ('SHA256=' + $h)"
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

REM ---- ZIP�i�t�H���_���Ɓj
if exist "%ZIP%" del "%ZIP%" >nul 2>&1
echo [INFO] ZIP �쐬: %ZIP%
powershell -NoProfile -Command ^
  "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%ZIP%' -Force"
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
