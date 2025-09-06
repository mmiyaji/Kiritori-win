@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================================
REM  Kiritori リリースパッケージ作成（ビルド→同梱→ZIP）
REM  使い方:
REM    1) ダブルクリック → バージョン入力 → 既定 no-pause
REM    2) コマンド:  release.bat 1.2.1 [--pause|--no-pause]
REM ============================================================

REM ---- 既定: 明示 --pause のときだけ pause、他は no-pause
set "PAUSE_AT_END="

if "%~1"=="" (
  echo バージョン番号を入力してください（例: 1.2.1）:
  set /p VERSION=Version: 
  if "!VERSION!"=="" (
    echo [ERROR] バージョンが指定されませんでした。
    goto :end_fail
  )
) else (
  set "VERSION=%~1"
)

REM ---- オプション (--pause / --no-pause)
if /I "%~2"=="--pause"     set "PAUSE_AT_END=1"
if /I "%~2"=="--no-pause"  set "PAUSE_AT_END="

REM ---- パス
set "ROOT=%~dp0"
set "SLN=%ROOT%Kiritori.sln"

REM ---- Git 短縮ハッシュ → InformationalVersion に付与（任意）
@REM for /f %%G in ('git rev-parse --short HEAD 2^>nul') do set "GITHASH=%%G"
if defined GITHASH (
  set "INFOVER=%VERSION%+g%GITHASH%"
) else (
  set "INFOVER=%VERSION%"
)

echo [INFO] Version        : %VERSION%  (IV=%INFOVER%)
echo [INFO] Solution       : %SLN%

REM ---- msbuild 検出（PATH → vswhere）
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
  echo [ERROR] msbuild が見つかりません。Developer Command Prompt で実行してください。
  goto :end_fail
)

REM ---- Rebuild（Release/Any CPU）＋ バージョン注入
echo [INFO] Building (Rebuild Release)...
"%MSBUILD_EXE%" "%SLN%" /t:Rebuild /v:m ^
  /p:Configuration=Release /p:Platform="Any CPU" ^
  /p:Version=%VERSION% ^
  /p:AssemblyVersion=%VERSION%.0 ^
  /p:FileVersion=%VERSION%.0 ^
  /p:InformationalVersion=%INFOVER%
if errorlevel 1 (
  echo [ERROR] ビルドに失敗しました。
  goto :end_fail
)

REM ---- 成果物の場所（優先: Package 出力 → 次: 直下の bin\Release）
set "BIN1=%ROOT%KiritoriPackage\bin\AnyCPU\Release\Kiritori"
set "BIN2=%ROOT%Kiritori\bin\Release"
set "BIN="
if exist "%BIN1%\Kiritori.exe" set "BIN=%BIN1%"
if not defined BIN if exist "%BIN2%\Kiritori.exe" set "BIN=%BIN2%"
if not defined BIN (
  echo [ERROR] 実行ファイルが見つかりません:
  echo        試行1: %BIN1%\Kiritori.exe
  echo        試行2: %BIN2%\Kiritori.exe
  goto :end_fail
)

set "EXE=%BIN%\Kiritori.exe"
set "SAT_JA_DIR=%BIN%\ja"

REM ---- ThirdParty（リポジトリ側のソース配置想定）
REM  例: Kiritori/ThirdParty/ffmpeg/ffmpeg.exe など
set "TP_SRC=%ROOT%Kiritori\ThirdParty"
set "TP_DST_REL=ThirdParty"                         REM OUTDIR 配下のフォルダ名
set "TP_DST="                                       REM 後で %OUTDIR%\%TP_DST_REL%

set "README_SRC=%ROOT%README.md"
set "LICENSE_SRC="
for %%F in ("%ROOT%LICENSE" "%ROOT%LICENSE.txt" "%ROOT%LICENSE.md") do (
  if not defined LICENSE_SRC if exist "%%~fF" set "LICENSE_SRC=%%~fF"
)

set "OUTBASE=%ROOT%dist"
set "OUTDIR=%OUTBASE%\Kiritori-%VERSION%"
set "ZIP=%OUTBASE%\Kiritori-%VERSION%.zip"

echo [INFO] Build Dir     : %BIN%
echo [INFO] Out Dir       : %OUTDIR%

REM ---- dist 上書き（同じバージョンでも必ず消して作り直す）
if not exist "%OUTBASE%" mkdir "%OUTBASE%" >nul 2>&1
if exist "%OUTDIR%" rd /s /q "%OUTDIR%" >nul 2>&1
mkdir "%OUTDIR%" || goto :end_fail
if exist "%ZIP%" del "%ZIP%" >nul 2>&1

REM ---- 配置（EXE / DLL / Config / サテライト）
copy /y "%EXE%" "%OUTDIR%\Kiritori.exe" >nul || goto :end_fail

if exist "%EXE%.config" (
  copy /y "%EXE%.config" "%OUTDIR%\Kiritori.exe.config" >nul
  echo [INFO] EXE.config を同梱しました。
)

robocopy "%BIN%" "%OUTDIR%" *.dll /R:0 /W:0 /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  echo [ERROR] DLL コピーに失敗しました。
  goto :end_fail
) else (
  echo [INFO] DLL を同梱しました（%BIN%\*.dll）。
)

if exist "%SAT_JA_DIR%\" (
  mkdir "%OUTDIR%\ja" >nul 2>&1
  xcopy /y /q "%SAT_JA_DIR%\*.resources.dll" "%OUTDIR%\ja\" >nul
  echo [INFO] ja サテライト DLL を同梱しました。
) else (
  echo [INFO] ja サテライト DLL は見つかりませんでした（スキップ）。
)

REM -----------------------------------------------------------------
REM  ThirdParty をそのまま同梱（ffmpeg.exe を含める）
REM -----------------------------------------------------------------
set "TP_DST=%OUTDIR%\%TP_DST_REL%"
if exist "%TP_SRC%\" (
  robocopy "%TP_SRC%" "%TP_DST%" *.* /E /R:0 /W:0 /NFL /NDL /NJH /NJS /NP >nul
  if errorlevel 8 (
    echo [ERROR] ThirdParty コピーに失敗しました。
    goto :end_fail
  ) else (
    echo [INFO] ThirdParty を同梱しました（%TP_DST_REL%\ 配下）。
  )
) else (
  echo [WARN ] ThirdParty フォルダが見つかりませんでした（同梱スキップ）。
)

REM -----------------------------------------------------------------
REM  LICENSE/README へ FFmpeg 節を自動追記
REM       - 既存 LICENSE があれば OUTDIR\LICENSE.txt としてコピーし追記
REM       - なければ新規作成して追記
REM       - README があれば末尾に Third-Party ライセンス節を追記
REM -----------------------------------------------------------------

REM --- LICENSE.txt の用意（自プロジェクトの LICENSE -> OUTDIR\LICENSE.txt）
set "OUT_LICENSE=%OUTDIR%\LICENSE.txt"
if defined LICENSE_SRC (
  copy /y "%LICENSE_SRC%" "%OUT_LICENSE%" >nul
) 
REM --- README.md のコピー（存在すれば）
if exist "%README_SRC%" (
  copy /y "%README_SRC%" "%OUTDIR%\README.md" >nul
)
REM --- ThirdParty/ffmpeg 側に原本ライセンスがあれば一緒に（任意）
if exist "%TP_SRC%\ffmpeg\LICENSE.txt" (
  copy /y "%TP_SRC%\ffmpeg\LICENSE.txt" "%TP_DST%\ffmpeg\LICENSE.txt" >nul
)
if exist "%TP_SRC%\ffmpeg\COPYING"* (
  copy /y "%TP_SRC%\ffmpeg\COPYING"* "%TP_DST%\ffmpeg\" >nul
)

REM ---- 埋め込まれたバージョンの確認（ログ出力用）
powershell -NoProfile -Command ^
  "$v=[System.Diagnostics.FileVersionInfo]::GetVersionInfo('%OUTDIR%\Kiritori.exe');" ^
  "Write-Host ('[INFO] FileVersion=' + $v.FileVersion + '  ProductVersion=' + $v.ProductVersion)"

REM ---- SHA-256
echo [INFO] 生成: Kiritori.exe.sha256
powershell -NoProfile -Command "$h=(Get-FileHash -Algorithm SHA256 -Path '%OUTDIR%\Kiritori.exe').Hash; $line=$h + ' *Kiritori.exe'; Set-Content -Path '%OUTDIR%\Kiritori.exe.sha256' -Value $line -Encoding ASCII; Write-Host ('SHA256=' + $h)"
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

REM ---- ZIP（フォルダごと。既存があれば上書き）
echo [INFO] ZIP 作成: %ZIP%
powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%ZIP%' -Force"
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
