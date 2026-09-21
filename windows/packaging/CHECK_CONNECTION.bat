@echo off
title HolyHand AI Gateway Check
cd /d "%~dp0"

echo Testing connection to Vercel AI Gateway (Jev model)...
echo.
"%~dp0HolyHand.Cli.exe" check
echo.
echo Press any key to close...
pause >nul
