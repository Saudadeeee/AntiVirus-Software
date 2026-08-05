@echo off
setlocal
rem ---------------------------------------------------------------------------
rem  AVAK release publish
rem  Produces a self-contained x64 build that runs on a PC with no .NET installed.
rem ---------------------------------------------------------------------------

set OUT=%~dp0dist
set RID=win-x64

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] The .NET SDK was not found on PATH.
    echo Install the .NET 8 SDK or newer: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/4] Running the unit tests...
dotnet test "%~dp0tests\AVAK.Tests\AVAK.Tests.vbproj" -c Release --nologo -v q
if errorlevel 1 (
    echo.
    echo [ERROR] Tests failed - refusing to publish.
    pause
    exit /b 1
)

echo.
echo [2/4] Publishing self-contained %RID%...
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish "%~dp0AVAK.vbproj" -c Release -r %RID% --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none ^
    -o "%OUT%" --nologo
if errorlevel 1 (
    echo.
    echo [ERROR] Publish failed.
    pause
    exit /b 1
)

echo.
echo [3/4] Running the engine self-test against the published build...
"%OUT%\AVAK.exe" --selftest
if errorlevel 1 (
    echo.
    echo [ERROR] The published build failed its self-test.
    pause
    exit /b 1
)

echo.
echo [4/4] Copying the documents users are entitled to...
copy /y "%~dp0LICENSE.txt"              "%OUT%\" >nul 2>nul
copy /y "%~dp0PRIVACY.md"               "%OUT%\" >nul 2>nul
copy /y "%~dp0THIRD-PARTY-NOTICES.md"   "%OUT%\" >nul 2>nul
copy /y "%~dp0README.md"                "%OUT%\" >nul 2>nul

echo.
echo [SUCCESS] Release build in: %OUT%
echo.
echo NEXT STEPS BEFORE SHIPPING TO REAL USERS
echo   1. Authenticode-sign AVAK.exe with an OV or EV certificate.
echo      signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a "%OUT%\AVAK.exe"
echo      Unsigned security software is blocked by SmartScreen and distrusted by users.
echo   2. Build the installer:  iscc installer\AVAK.iss
echo   3. Sign the installer as well.
echo   4. content\ and plugins\ ship next to the exe - rules stay editable after install.
echo.
pause
