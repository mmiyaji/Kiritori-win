@echo off
setlocal EnableExtensions EnableDelayedExpansion
pushd "%~dp0"
REM ============================================================
REM  Kiritori: Release & Store Builder (APPX3104-safe)
REM  - Prebuild Kiritori.csproj (Any CPU) ONLY
REM  - Publish wapproj separately with Platform=x64
REM  - Avoids building the .sln (which may pull AnyCPU wapproj)
REM ============================================================

REM -------- Parse args
set "VERSION=%~1"
set "MODE="
set "PAUSE_AT_END="

for %%A in (%*) do (
  if /I "%%~A"=="--zip-only" set "MODE=ZIP"
  if /I "%%~A"=="--store-only" set "MODE=STORE"
  if /I "%%~A"=="--pause" set "PAUSE_AT_END=1"
)

if "%VERSION%"=="" (
  echo "Enter version (e.g., 1.5.0):"
  set /p VERSION=Version:
)

if "%VERSION%"=="" (
  echo "[ERROR] Version is required."
  goto :end_fail
)

REM -------- Project layout
set "CS_PROJ=Kiritori\Kiritori.csproj"
set "WAP_PROJ=KiritoriPackage\KiritoriPackage.wapproj"
set "APPX_MAN=KiritoriPackage\Package.appxmanifest"

REM -------- Output layout
set "DIST=dist\%VERSION%"
set "ZIP_DIR=%DIST%\zip"
set "STORE_DIR=%DIST%\store"
set "TOP_DIST=dist"

for %%D in ("%DIST%" "%ZIP_DIR%" "%STORE_DIR%" "%TOP_DIST%") do if not exist "%%~D" mkdir "%%~D"

REM -------- Locate MSBuild
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
  echo "[WARN] vswhere not found. Trying msbuild on PATH..."
  where msbuild >nul 2>nul || (
    echo "[ERROR] Neither vswhere nor msbuild found. Install Visual Studio Build Tools."
    goto :end_fail
  )
  for /f "usebackq tokens=*" %%I in (`where msbuild`) do set "MSBUILD=%%~I"
) else (
  for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%~I"
)

if not exist "%MSBUILD%" (
  echo "[ERROR] MSBuild not found."
  goto :end_fail
)

echo "[INFO] MSBuild: %MSBUILD%"

REM -------- Build parameters
set "CONF=Release"
set "PLAT=x64"

REM ============================================================
REM  0) Build Kiritori.csproj ONLY (Any CPU) with /p:Version
REM ============================================================
if /I not "%MODE%"=="STORE" (
  echo.
  echo "[STEP] Building Kiritori.csproj (Release|x64) with version !VERSION!..."
  "%MSBUILD%" "%CS_PROJ%" /t:Build /m /p:Configuration=!CONF! /p:Platform="x64" /p:Version=!VERSION!
  if errorlevel 1 (
    echo "[ERROR] Project build failed."
    goto :end_fail
  )
)

REM ============================================================
REM  1) Bump MSIX version in Package.appxmanifest
REM ============================================================
set "PKG_VER=!VERSION!.0"
if /I not "%MODE%"=="ZIP" (
  echo.
  echo "[STEP] Updating Package.appxmanifest version to !PKG_VER!..."
  if not exist "!APPX_MAN!" (
    echo "[ERROR] Not found: !APPX_MAN!"
    goto :end_fail
  )
  copy /y "!APPX_MAN!" "!APPX_MAN!.bak" >nul

  powershell -NoProfile -Command ^
    "$p = Resolve-Path '!APPX_MAN!';" ^
    "$xml = [xml](Get-Content $p);" ^
    "$xml.Package.Identity.Version = '!PKG_VER!';" ^
    "$xml.Save($p)"
  if errorlevel 1 (
    echo "[ERROR] Failed to update manifest version. Reverting..."
    copy /y "!APPX_MAN!.bak" "!APPX_MAN!" >nul
    goto :end_fail
  )
  echo "[INFO] Package.appxmanifest updated."
)

REM ============================================================
REM  2) Publish wapproj (x64 only) ? APPX3104-safe
REM ============================================================
if /I not "%MODE%"=="ZIP" (
  echo.
  echo "[STEP] Publishing MSIX (Store upload, x64 only)..."
  "%MSBUILD%" "%WAP_PROJ%" /t:Restore,Publish /m ^
    /p:Configuration=!CONF! ^
    /p:Platform=!PLAT! ^
    /p:AppxBundle=Always ^
    /p:AppxBundlePlatforms=x64 ^
    /p:UapAppxPackageBuildMode=StoreUpload ^
    /p:AppxPackageSigningEnabled=false ^
    /p:BuildProjectReferences=false
  if errorlevel 1 (
    echo "[ERROR] Store package build failed."
    goto :restore_manifest
  )

  echo "[STEP] Moving store artifacts to '!STORE_DIR!'..."
  if not exist "!STORE_DIR!" mkdir "!STORE_DIR!"
  for %%F in (KiritoriPackage\**\bin\!PLAT!\!CONF!\*.*) do (
    rem no-op; just ensure path exists
  )
  for %%F in (KiritoriPackage\bin\!PLAT!\!CONF!\*.msix* KiritoriPackage\bin\!PLAT!\!CONF!\*.appx* KiritoriPackage\bin\!PLAT!\!CONF!\*.eappx* KiritoriPackage\bin\!PLAT!\!CONF!\*.msixbundle*) do (
    if exist "%%~fF" move /y "%%~fF" "!STORE_DIR!\">nul
  )
  dir /b "!STORE_DIR!"
)

REM ============================================================
REM  3) Create App ZIP from packaged output (preferred)
REM ============================================================
set "APP_ZIP="
if /I not "%MODE%"=="STORE" (
  echo.
  echo "[STEP] Creating app ZIP package from packaged output..."
  set "APP_OUT=KiritoriPackage\bin\!PLAT!\!CONF!\Kiritori"
  if not exist "!APP_OUT!\Kiritori.exe" (
    echo "[WARN] Not found packaged output: !APP_OUT!\Kiritori.exe"
    echo "[INFO] Falling back to project output: Kiritori\bin\Any CPU\!CONF!"
    set "APP_OUT=Kiritori\bin\Any CPU\!CONF!"
  )

  if not exist "!APP_OUT!\Kiritori.exe" (
    echo "[ERROR] Built binary not found: !APP_OUT!\Kiritori.exe"
    goto :restore_manifest
  )

  set "ZIP_STAGING=!ZIP_DIR!\Kiritori-!VERSION!"
  if exist "!ZIP_STAGING!" rmdir /s /q "!ZIP_STAGING!"
  mkdir "!ZIP_STAGING!"

  xcopy /e /i /y "!APP_OUT!\*" "!ZIP_STAGING!\" >nul
  del /q "!ZIP_STAGING!\*.pdb" 2>nul
  del /q "!ZIP_STAGING!\*.xml" 2>nul

  if exist "Kiritori\ThirdParty" (
    xcopy /e /i /y "Kiritori\ThirdParty\*" "!ZIP_STAGING!\ThirdParty\" >nul
  )

  set "APP_ZIP=!ZIP_DIR!\Kiritori-!VERSION!.zip"
  if exist "!APP_ZIP!" del "!APP_ZIP!"
  powershell -NoProfile -Command "Compress-Archive -Path '!ZIP_STAGING!\*' -DestinationPath '!APP_ZIP!' -Force"
  if errorlevel 1 (
    echo "[ERROR] Compress-Archive failed."
    goto :restore_manifest
  )
  echo "[INFO] App ZIP created: !APP_ZIP!"

  for /f "usebackq tokens=*" %%H in (`powershell -NoProfile -Command "(Get-FileHash -Algorithm SHA256 '!APP_ZIP!').Hash"`) do set "APP_SHA=%%H"
  echo "[SHA256] !APP_ZIP! = !APP_SHA!"
)

REM ============================================================
REM  4) Language ZIP(s) + SHA256
REM ============================================================
if /I not "%MODE%"=="STORE" (
  echo.
  echo "[STEP] Creating language ZIP(s)..."
  set "LANGS=ja"
  for %%L in (!LANGS!) do (
    set "RES=Kiritori\i18n\%%L\Kiritori.resources.dll"
    if exist "!RES!" (
      set "LANG_ZIP=!TOP_DIST!\lang-%%L-!VERSION!.zip"
      if exist "!LANG_ZIP!" del "!LANG_ZIP!"
      powershell -NoProfile -Command "Compress-Archive -Path '!RES!' -DestinationPath '!LANG_ZIP!' -Force"
      if errorlevel 1 (
        echo "[ERROR] Compress-Archive failed for %%L."
        goto :restore_manifest
      )
      echo "[INFO] Lang ZIP created: !LANG_ZIP!"
      for /f "usebackq tokens=*" %%H in (`powershell -NoProfile -Command "(Get-FileHash -Algorithm SHA256 '!LANG_ZIP!').Hash"`) do set "LANG_SHA=%%H"
      echo "[SHA256] !LANG_ZIP! = !LANG_SHA!"
    ) else (
      echo "[WARN] Not found: !RES!"
    )
  )
)

REM ============================================================
REM  5) Checksums file
REM ============================================================
echo.
echo "[STEP] Generating SHA256 checksums file..."
set "CS_OUT=!DIST!\checksums.txt"
if exist "!CS_OUT!" del "!CS_OUT!"

for %%F in ("!ZIP_DIR!\*.zip") do powershell -NoProfile -Command "$h=(Get-FileHash -Algorithm SHA256 '%%~fF').Hash; Add-Content -Path '!CS_OUT!' -Value ('{0}  {1}' -f $h,'%%~nxF')"
for %%F in ("!TOP_DIST!\lang-*-!VERSION!.zip") do if exist "%%~fF" powershell -NoProfile -Command "$h=(Get-FileHash -Algorithm SHA256 '%%~fF').Hash; Add-Content -Path '!CS_OUT!' -Value ('{0}  {1}' -f $h,'%%~nxF')"
for %%F in ("!STORE_DIR!\*.msix*" "!STORE_DIR!\*.appx*" "!STORE_DIR!\*.eappx*" "!STORE_DIR!\*.msixbundle*") do if exist "%%~fF" powershell -NoProfile -Command "$h=(Get-FileHash -Algorithm SHA256 '%%~fF').Hash; Add-Content -Path '!CS_OUT!' -Value ('{0}  {1}' -f $h,'%%~nxF')"

type "!CS_OUT!"

:restore_manifest
if exist "!APPX_MAN!.bak" (
  copy /y "!APPX_MAN!.bak" "!APPX_MAN!" >nul
  del /q "!APPX_MAN!.bak" >nul 2>nul
)

echo.
echo "[DONE] Artifacts ready. Version: !VERSION!"
if defined PAUSE_AT_END pause
popd
exit /b 0

:end_fail
if exist "!APPX_MAN!.bak" (
  copy /y "!APPX_MAN!.bak" "!APPX_MAN!" >nul
  del /q "!APPX_MAN!.bak" >nul 2>nul
)
popd
exit /b 1
