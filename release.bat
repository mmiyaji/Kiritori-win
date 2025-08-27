@echo off
setlocal
REM ============================================================
REM  Kiritori �����[�X�p�b�P�[�W�쐬�o�b�`
REM  �g����:  release.bat 1.1.1
REM  �o��:   dist\Kiritori-1.1.1\ (exe, README.md, exe�̃n�b�V��)
REM          dist\Kiritori-1.1.1.zip  �i�t�H���_����ZIP�j
REM  �����Ȃ��Ȃ�Θb�I�ɓ���
REM ============================================================

if "%~1"=="" (
  echo �o�[�W�����ԍ�����͂��Ă��������i��: 1.1.1�j:
  set /p VERSION=Version: 
  if "%VERSION%"=="" (
    echo [ERROR] �o�[�W�������w�肳��܂���ł����B
    exit /b 1
  )
) else (
  set "VERSION=%~1"
)
echo [INFO] �o�[�W����: %VERSION%
set "ROOT=%~dp0"
set "EXE=%ROOT%Kiritori\bin\Release\Kiritori.exe"
set "README=%ROOT%README.md"
set "OUTBASE=%ROOT%dist"
set "OUTDIR=%OUTBASE%\Kiritori-%VERSION%"
set "ZIP=%OUTBASE%\Kiritori-%VERSION%.zip"

REM ���͊m�F
if not exist "%EXE%" (
  echo [ERROR] ���s�t�@�C����������܂���: %EXE%
  exit /b 1
)
if not exist "%README%" (
  echo [WARN ] README.md ��������܂���: %README%
  echo        README �̓������X�L�b�v���܂�
)

REM �o�̓f�B���N�g������
if not exist "%OUTBASE%" mkdir "%OUTBASE%" >nul 2>&1
if exist "%OUTDIR%" rd /s /q "%OUTDIR%" >nul 2>&1
mkdir "%OUTDIR%" || goto :fail

REM �t�@�C���z�u
copy /y "%EXE%" "%OUTDIR%\Kiritori.exe" >nul || goto :fail
if exist "%README%" copy /y "%README%" "%OUTDIR%\README.md" >nul

REM SHA-256 �n�b�V���쐬�iPowerShell�j
echo [INFO] ����: Kiritori.exe.sha256
powershell -NoProfile -Command ^
  "$h=(Get-FileHash -Algorithm SHA256 -Path '%OUTDIR%\Kiritori.exe').Hash;" ^
  "$line=$h + ' *Kiritori.exe';" ^
  "Set-Content -Path '%OUTDIR%\Kiritori.exe.sha256' -Value $line -Encoding ASCII;" ^
  "Write-Host ('SHA256=' + $h)"

if errorlevel 1 (
  echo [WARN ] PowerShell �ł̃n�b�V�������Ɏ��s�Bcertutil �������܂��c
  for /f "usebackq tokens=1 delims=" %%A in (`certutil -hashfile "%OUTDIR%\Kiritori.exe" SHA256 ^| find /i /r "[0-9A-F]"`) do (
    set "HASH=%%A"
  )
  if not defined HASH (
    echo [ERROR] �n�b�V�������Ɏ��s���܂����B
    goto :fail
  )
  > "%OUTDIR%\Kiritori.exe.sha256" echo %HASH% *Kiritori.exe
  echo [INFO] SHA256=%HASH%
)

REM ZIP ���i�t�H���_���Ɓj
if exist "%ZIP%" del "%ZIP%" >nul 2>&1

echo [INFO] ZIP �쐬: %ZIP%
powershell -NoProfile -Command ^
  "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%ZIP%' -Force"

if errorlevel 1 (
  echo [WARN ] PowerShell �̈��k�Ɏ��s�Btar �������܂��c
  tar.exe -a -c -f "%ZIP%" -C "%OUTBASE%" "Kiritori-%VERSION%"
  if errorlevel 1 goto :fail
)

echo.
echo [DONE] �o��:
echo   %OUTDIR%
echo   %ZIP%
exit /b 0

:fail
echo [FAIL] �r���ŃG���[���������܂����B
exit /b 1
