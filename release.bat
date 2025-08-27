@echo off
setlocal
REM ============================================================
REM  Kiritori リリースパッケージ作成バッチ
REM  使い方:  release.bat 1.1.1
REM  出力:   dist\Kiritori-1.1.1\ (exe, README.md, exeのハッシュ)
REM          dist\Kiritori-1.1.1.zip  （フォルダごとZIP）
REM  引数なしなら対話的に入力
REM ============================================================

if "%~1"=="" (
  echo バージョン番号を入力してください（例: 1.1.1）:
  set /p VERSION=Version: 
  if "%VERSION%"=="" (
    echo [ERROR] バージョンが指定されませんでした。
    exit /b 1
  )
) else (
  set "VERSION=%~1"
)
echo [INFO] バージョン: %VERSION%
set "ROOT=%~dp0"
set "EXE=%ROOT%Kiritori\bin\Release\Kiritori.exe"
set "README=%ROOT%README.md"
set "OUTBASE=%ROOT%dist"
set "OUTDIR=%OUTBASE%\Kiritori-%VERSION%"
set "ZIP=%OUTBASE%\Kiritori-%VERSION%.zip"

REM 入力確認
if not exist "%EXE%" (
  echo [ERROR] 実行ファイルが見つかりません: %EXE%
  exit /b 1
)
if not exist "%README%" (
  echo [WARN ] README.md が見つかりません: %README%
  echo        README の同梱をスキップします
)

REM 出力ディレクトリ準備
if not exist "%OUTBASE%" mkdir "%OUTBASE%" >nul 2>&1
if exist "%OUTDIR%" rd /s /q "%OUTDIR%" >nul 2>&1
mkdir "%OUTDIR%" || goto :fail

REM ファイル配置
copy /y "%EXE%" "%OUTDIR%\Kiritori.exe" >nul || goto :fail
if exist "%README%" copy /y "%README%" "%OUTDIR%\README.md" >nul

REM SHA-256 ハッシュ作成（PowerShell）
echo [INFO] 生成: Kiritori.exe.sha256
powershell -NoProfile -Command ^
  "$h=(Get-FileHash -Algorithm SHA256 -Path '%OUTDIR%\Kiritori.exe').Hash;" ^
  "$line=$h + ' *Kiritori.exe';" ^
  "Set-Content -Path '%OUTDIR%\Kiritori.exe.sha256' -Value $line -Encoding ASCII;" ^
  "Write-Host ('SHA256=' + $h)"

if errorlevel 1 (
  echo [WARN ] PowerShell でのハッシュ生成に失敗。certutil を試します…
  for /f "usebackq tokens=1 delims=" %%A in (`certutil -hashfile "%OUTDIR%\Kiritori.exe" SHA256 ^| find /i /r "[0-9A-F]"`) do (
    set "HASH=%%A"
  )
  if not defined HASH (
    echo [ERROR] ハッシュ生成に失敗しました。
    goto :fail
  )
  > "%OUTDIR%\Kiritori.exe.sha256" echo %HASH% *Kiritori.exe
  echo [INFO] SHA256=%HASH%
)

REM ZIP 化（フォルダごと）
if exist "%ZIP%" del "%ZIP%" >nul 2>&1

echo [INFO] ZIP 作成: %ZIP%
powershell -NoProfile -Command ^
  "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%ZIP%' -Force"

if errorlevel 1 (
  echo [WARN ] PowerShell の圧縮に失敗。tar を試します…
  tar.exe -a -c -f "%ZIP%" -C "%OUTBASE%" "Kiritori-%VERSION%"
  if errorlevel 1 goto :fail
)

echo.
echo [DONE] 出力:
echo   %OUTDIR%
echo   %ZIP%
exit /b 0

:fail
echo [FAIL] 途中でエラーが発生しました。
exit /b 1
