@echo off
REM Build script for Chat System (Windows)
REM This script builds all components of the chat system

echo =========================================
echo Building Chat System Components
echo =========================================

REM Build Shared Library
echo.
echo [1/4] Building Shared Library...
dotnet build Mostlylucid.Chat.Shared\Mostlylucid.Chat.Shared.csproj -c Release
if %ERRORLEVEL% NEQ 0 goto :error
echo [OK] Shared library built

REM Build Server
echo.
echo [2/4] Building Chat Server...
dotnet build Mostlylucid.Chat.Server\Mostlylucid.Chat.Server.csproj -c Release
if %ERRORLEVEL% NEQ 0 goto :error
echo [OK] Chat server built

REM Build Widget
echo.
echo [3/4] Building Chat Widget...
cd Mostlylucid.Chat.Widget

if not exist "node_modules\" (
    echo Installing npm dependencies...
    call npm install
    if %ERRORLEVEL% NEQ 0 goto :error
)

call npm run build
if %ERRORLEVEL% NEQ 0 goto :error
echo [OK] Chat widget built

REM Copy widget to server wwwroot
echo Copying widget to server...
if not exist "..\Mostlylucid.Chat.Server\wwwroot\" mkdir ..\Mostlylucid.Chat.Server\wwwroot
copy /Y dist\widget.js ..\Mostlylucid.Chat.Server\wwwroot\
if %ERRORLEVEL% NEQ 0 goto :error
echo [OK] Widget copied to server

cd ..

REM Build Tray App
echo.
echo [4/4] Building Tray App...
dotnet build Mostlylucid.Chat.TrayApp\Mostlylucid.Chat.TrayApp.csproj -c Release
if %ERRORLEVEL% NEQ 0 goto :error
echo [OK] Tray app built

echo.
echo =========================================
echo Build completed successfully!
echo =========================================
echo.
echo Next steps:
echo   1. Start the server: cd Mostlylucid.Chat.Server ^&^& dotnet run
echo   2. Open example: Mostlylucid.Chat.Widget\examples\index.html
echo   3. Run tray app: cd Mostlylucid.Chat.TrayApp ^&^& dotnet run

goto :end

:error
echo.
echo =========================================
echo Build FAILED!
echo =========================================
exit /b 1

:end
