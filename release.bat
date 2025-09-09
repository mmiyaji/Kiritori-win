@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM ============================================================
REM  Kiritori リリースパッケージ作成（ビルド→同梱→ZIP）
REM  使い方:
REM    1) ダブルクリック → バージョン入力 → 既定 no-pause
REM    2) コマンド:  release.bat 1.2.1 [--pause|--no-pause] [--aio] [--portable|--portable-marker-only] [--with-ja]
REM ============================================================

REM ---- 既定: 明示 --pause のときだけ pause、他は no-pause
set "PAUSE_AT_END="

REM ---- フラグ（既定オフ）
set "AIO="
set "PORTABLE="
set "PORTABLE_MARKER_ONLY="
set "WITH_JA="

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

REM ---- オプション (--pause / --no-pause など)
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
echo [INFO] Flags          : AIO=%AIO%  PORTABLE=%PORTABLE%  PORTABLE_MARKER_ONLY=%PORTABLE_MARKER_ONLY%  WITH_JA=%WITH_JA%

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
set "TP_SRC=%ROOT%Kiritori\ThirdParty"
set "TP_DST_REL=ThirdParty"
set "TP_DST="

set "README_SRC=%ROOT%README.md"
set "LICENSE_SRC="
for %%F in ("%ROOT%LICENSE" "%ROOT%LICENSE.txt" "%ROOT%LICENSE.md") do (
  if not defined LICENSE_SRC if exist "%%~fF" set "LICENSE_SRC=%%~fF"
)

REM ---- 出力名をフラグに応じて決定
set "OUTBASE=%ROOT%dist"
set "OUTNAME=Kiritori-%VERSION%"
if defined PORTABLE set "OUTNAME=%OUTNAME%-portable"
if defined AIO set "OUTNAME=%OUTNAME%-aio"
set "OUTDIR=%OUTBASE%\%OUTNAME%"
set "ZIP=%OUTBASE%\%OUTNAME%.zip"

echo [INFO] Build Dir     : %BIN%
echo [INFO] Out Dir       : %OUTDIR%

REM ---- dist 上書き（同じバージョンでも必ず消して作り直す）
if not exist "%OUTBASE%" mkdir "%OUTBASE%" >nul 2>&1
if exist "%OUTDIR%" rd /s /q "%OUTDIR%" >nul 2>&1
mkdir "%OUTDIR%" || goto :end_fail
if exist "%ZIP%" del "%ZIP%" >nul 2>&1

REM ---- 配置（EXE / Config / DLL）
set "EXENAME=Kiritori.exe"
copy /y "%EXE%" "%OUTDIR%\%EXENAME%" >nul || goto :end_fail

if exist "%EXE%.config" (
  copy /y "%EXE%.config" "%OUTDIR%\Kiritori.exe.config" >nul
  echo [INFO] EXE.config を同梱しました。
)

@REM REM DLL（ルート直下 *.dll のみ）
@REM robocopy "%BIN%" "%OUTDIR%" *.dll /R:0 /W:0 /NFL /NDL /NJH /NJS /NP >nul
@REM if errorlevel 8 (
@REM   echo [ERROR] DLL コピーに失敗しました。
@REM   goto :end_fail
@REM ) else (
@REM   echo [INFO] DLL を同梱しました（%BIN%\*.dll）。
@REM )

REM ---- 拡張管理DLLの明示除去（拡張からロードさせる想定のもの）
for %%F in (
  CommunityToolkit.WinUI.Notifications.dll
) do (
  if exist "%OUTDIR%\%%~F" del /f /q "%OUTDIR%\%%~F" >nul
)

REM ---- ja サテライト（必要時のみ）
if defined WITH_JA (
  if exist "%SAT_JA_DIR%\" (
    mkdir "%OUTDIR%\ja" >nul 2>&1
    xcopy /y /q "%SAT_JA_DIR%\*.resources.dll" "%OUTDIR%\ja\" >nul
    echo [INFO] ja サテライト DLL を同梱しました。
  ) else (
    echo [INFO] ja サテライト DLL は見つかりませんでした（スキップ）。
  )
) else (
  echo [INFO] ja サテライトは拡張で提供（同梱しません）。
)

REM ---- ThirdPartyManifests（外部JSONを同梱：無ければ埋め込みで起動可）
if exist "%BIN%\ThirdPartyManifests\" (
  robocopy "%BIN%\ThirdPartyManifests" "%OUTDIR%\ThirdPartyManifests" *.json /R:0 /W:0 /NFL /NDL /NJH /NJS /NP >nul
  echo [INFO] ThirdPartyManifests を同梱しました（外部JSON／埋め込みどちらでも起動可）。
) else (
  echo [INFO] ThirdPartyManifests は見つかりません（埋め込みにフォールバック）。
)

REM -----------------------------------------------------------------
REM  ThirdParty をそのまま同梱（ffmpeg.exe 等）… AIO のときだけ
REM -----------------------------------------------------------------
if defined AIO (
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
) else (
  echo [INFO] AIO ではないため ThirdParty は同梱しません。
)

REM ---- LICENSE/README コピー
set "OUT_LICENSE=%OUTDIR%\LICENSE.txt"
if defined LICENSE_SRC (
  copy /y "%LICENSE_SRC%" "%OUT_LICENSE%" >nul
)
if exist "%README_SRC%" (
  copy /y "%README_SRC%" "%OUTDIR%\README.md" >nul
)

REM ---- AIO の場合は ThirdParty\ffmpeg の LICENSE も添付（あれば）
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

REM ---- ポータブルマーカー＆EXE名変更
if defined PORTABLE_MARKER_ONLY (
  break > "%OUTDIR%\Kiritori.portable"
)
if defined PORTABLE (
  break > "%OUTDIR%\Kiritori.portable"
  ren "%OUTDIR%\%EXENAME%" "Kiritori-portable.exe"
  set "EXENAME=Kiritori-portable.exe"
)

REM ---- 埋め込まれたバージョンの確認（ログ出力用）
powershell -NoProfile -Command ^
  "$v=[System.Diagnostics.FileVersionInfo]::GetVersionInfo('%OUTDIR%\%EXENAME%');" ^
  "Write-Host ('[INFO] FileVersion=' + $v.FileVersion + '  ProductVersion=' + $v.ProductVersion)"

REM ---- SHA-256（EXE名に追従）
echo [INFO] 生成: %EXENAME%.sha256
powershell -NoProfile -Command "$h=(Get-FileHash -Algorithm SHA256 -Path '%OUTDIR%\%EXENAME%').Hash; $line=$h + ' *%EXENAME%'; Set-Content -Path '%OUTDIR%\%EXENAME%.sha256' -Value $line -Encoding ASCII; Write-Host ('SHA256=' + $h)"
if errorlevel 1 (
  echo [WARN ] PowerShell 失敗。certutil を試します…
  for /f "usebackq tokens=1" %%A in (`certutil -hashfile "%OUTDIR%\%EXENAME%" SHA256 ^| findstr /r /i "^[0-9A-F][0-9A-F]*$"`) do set "HASH=%%A"
  if not defined HASH (
    echo [ERROR] ハッシュ生成に失敗しました。
    goto :end_fail
  )
  > "%OUTDIR%\%EXENAME%.sha256" echo %HASH% *%EXENAME%
  echo [INFO] SHA256=%HASH%
)

REM ---- ZIP（フォルダごと。既存があれば上書き）
echo [INFO] ZIP 作成: %ZIP%
powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 (
  echo [WARN ] PowerShell の圧縮に失敗。tar を試します…
  tar.exe -a -c -f "%ZIP%" -C "%OUTBASE%" "%OUTNAME%"
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
