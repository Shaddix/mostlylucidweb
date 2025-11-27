@echo off
REM Quick start script for Chat Server (Windows)
REM This script starts the chat server with appropriate settings

echo Starting Chat Server...
echo =======================
echo.
echo Server will be available at: http://localhost:5100
echo Widget endpoint: http://localhost:5100/widget.js
echo Health check: http://localhost:5100/health
echo SignalR hub: http://localhost:5100/chathub
echo.
echo Press Ctrl+C to stop the server
echo.

cd Mostlylucid.Chat.Server
dotnet run
