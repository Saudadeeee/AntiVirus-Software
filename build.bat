@echo off
setlocal
echo ===================================================
echo   AVAK - build script (no Visual Studio required)
echo ===================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] The .NET SDK was not found on PATH.
    echo Install the .NET 8 SDK or newer:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

for /f "delims=" %%v in ('dotnet --version') do set SDKVER=%%v
echo [INFO] .NET SDK %SDKVER%
echo [INFO] Building AVAK (Release)...
echo.

dotnet build "%~dp0AVAK.vbproj" -c Release -nologo
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed. See the messages above.
    echo.
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Build finished.
echo Executable: %~dp0bin\Release\net8.0-windows\AVAK.exe
echo.
echo   AVAK.exe                     start the app
echo   AVAK.exe --scan "C:\folder"  headless scan (exit code = threats found)
echo   AVAK.exe --uitest .\shots    render every page to PNG (self-test)
echo.
pause
