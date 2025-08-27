@echo off
setlocal EnableExtensions
REM ============================================================
REM  Kiritori リリースパッケージ作成
REM  使い方:
REM    1) ダブルクリック → バージョンを聞かれる → 終了時に pause
REM    2) コマンドライン:  release.bat 1.1.3 [--pause|--no-pause]
REM ============================================================

REM ---- 既定の動作: 対話実行(引数なし)なら pause、引数ありなら no-pause
set "PAUSE_AT_END="

if "%~1"=="" (
  echo バージョン番号を入力してください（例: 1.1.3）:
  set /p VERSION=Version: 
  if "%VERSION%"=="" (
    echo [ERROR] バージョンが指定されませんでした。
    set "PAUSE_AT_END=1"
    goto :end_fail
  )
  set "PAUSE_AT_END=1"
) else (
  set "VERSION=%~1"
)

REM ---- オプション (--pause / --no-pause)
if /I "%~2"=="--pause"     set "PAUSE_AT_END=1"
if /I "%~2"=="--no-pause"  set "PAUSE_AT_END="

REM ---- パス
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

REM ---- 入力確認
if not exist "%EXE%" (
  echo [ERROR] 実行ファイルが見つかりません: %EXE%
  set "PAUSE_AT_END=1"
  goto :end_fail
)

REM ---- 出力ディレクトリの用意
if not exist "%OUTBASE%" mkdir "%OUTBASE%" >nul 2>&1
if exist "%OUTDIR%" rd /s /q "%OUTDIR%" >nul 2>&1
mkdir "%OUTDIR%" || goto :end_fail

REM ---- ファイル配置
copy /y "%EXE%" "%OUTDIR%\Kiritori.exe" >nul || goto :end_fail
if exist "%EXE_CFG%" copy /y "%EXE_CFG%" "%OUTDIR%\Kiritori.exe.config" >nul

if exist "%README%" (
  copy /y "%README%" "%OUTDIR%\README.md" >nul
) else (
  echo [WARN ] README.md が見つかりませんでした（同梱スキップ）。
)

REM ---- サテライトDLL(ja) 同梱
if exist "%SAT_JA_DIR%\" (
  mkdir "%OUTDIR%\ja" >nul 2>&1
  xcopy /y /q "%SAT_JA_DIR%\*.resources.dll" "%OUTDIR%\ja\" >nul
  echo [INFO] ja サテライト DLL を同梱しました。
) else (
  echo [INFO] ja サテライト DLL は見つかりませんでした（スキップ）。
)

REM ---- SHA-256 ハッシュ
echo [INFO] 生成: Kiritori.exe.sha256
powershell -NoProfile -Command ^
  "$h=(Get-FileHash -Algorithm SHA256 -Path '%OUTDIR%\Kiritori.exe').Hash;" ^
  "$line=$h + ' *Kiritori.exe';" ^
  "Set-Content -Path '%OUTDIR%\Kiritori.exe.sha256' -Value $line -Encoding ASCII;" ^
  "Write-Host ('SHA256=' + $h)"
if errorlevel 1 (
  echo [WARN ] PowerShell 失敗。certutil を試します…
  for /f "usebackq tokens=1" %%A in (`certutil -hashfile "%OUTDIR%\Kiritori.exe" SHA256 ^| findstr /r /i "^[0-9A-F][0-9A-F]*$"`) do set "HASH=%%A"
  if not defined HASH (
    echo [ERROR] ハッシュ生成に失敗しました。
    goto :end_fail
  )
  > "%OUTDIR%\Kiritori.exe.sha256" echo %HASH% *Kiritori.exe
  echo [INFO] SHA256=%HASH%
)

REM ---- ZIP（フォルダごと）
if exist "%ZIP%" del "%ZIP%" >nul 2>&1
echo [INFO] ZIP 作成: %ZIP%
powershell -NoProfile -Command ^
  "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 (
  echo [WARN ] PowerShell の圧縮に失敗。tar を試します…
  tar.exe -a -c -f "%ZIP%" -C "%OUTBASE%" "Kiritori-%VERSION%"
  if errorlevel 1 goto :end_fail
)

echo.
echo [DONE] 出力:
echo   %OUTDIR%
echo   %ZIP%
goto :end_ok

:end_fail
echo.
echo [FAIL] 途中でエラーが発生しました。
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
