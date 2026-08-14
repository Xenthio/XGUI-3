@echo off
setlocal

if "%~1"=="" goto :usage
if "%~2"=="" goto :usage

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0splice_endcaps_fill.ps1" "%~1" "%~2"
set "exitCode=%ERRORLEVEL%"

echo.
if not "%exitCode%"=="0" echo Splicing failed with exit code %exitCode%.
pause
exit /b %exitCode%

:usage
echo Drag two image files onto this BAT file.
echo The wider image is split into left and right endcaps, and the narrower image is inserted between them.
pause
exit /b 2
