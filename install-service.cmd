@echo off
REM MsSqlMCP Windows Service Installation Script (CMD wrapper)
REM Run as Administrator

echo ============================================
echo MsSqlMCP Windows Service Installer
echo ============================================
echo.

REM Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

REM Get the directory where this script is located
set SCRIPT_DIR=%~dp0
set INSTALL_PATH=C:\Services\MsSqlMCP
set SERVICE_NAME=MsSqlMCP

echo Script Directory: %SCRIPT_DIR%
echo Install Path: %INSTALL_PATH%
echo.

REM Check if service already exists
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% equ 0 (
    echo Service %SERVICE_NAME% already exists.
    echo.
    choice /C YN /M "Do you want to reinstall"
    if errorlevel 2 goto :end
    
    echo Stopping and removing existing service...
    net stop %SERVICE_NAME% >nul 2>&1
    sc delete %SERVICE_NAME%
    timeout /t 3 >nul
)

REM Check if published files exist
set PUBLISH_DIR=%SCRIPT_DIR%bin\Release\net10.0\win-x64\publish
if not exist "%PUBLISH_DIR%" (
    echo Publishing application...
    cd /d "%SCRIPT_DIR%"
    dotnet publish -c Release -r win-x64 --self-contained true
    if errorlevel 1 (
        echo ERROR: Failed to publish application.
        pause
        exit /b 1
    )
)

REM Create installation directory
if not exist "%INSTALL_PATH%" (
    echo Creating installation directory...
    mkdir "%INSTALL_PATH%"
)

REM Copy files
echo Copying files to %INSTALL_PATH%...
xcopy "%PUBLISH_DIR%\*" "%INSTALL_PATH%\" /E /Y /Q

REM Create logs directory
if not exist "%INSTALL_PATH%\logs" mkdir "%INSTALL_PATH%\logs"

REM Create the service
echo Creating Windows Service...
sc create %SERVICE_NAME% binPath= "\"%INSTALL_PATH%\MsSqlMCP.exe\" --http-only" start= auto DisplayName= "MsSql MCP Server"
sc description %SERVICE_NAME% "Model Context Protocol server for SQL Server database inspection"

echo.
echo ============================================
echo Installation Complete!
echo ============================================
echo.
echo Service Name: %SERVICE_NAME%
echo Install Path: %INSTALL_PATH%
echo Service URL:  http://localhost:5000/sse
echo.
echo Next steps:
echo   1. Review configuration: %INSTALL_PATH%\appsettings.json
echo   2. Start service: net start %SERVICE_NAME%
echo   3. Check logs: %INSTALL_PATH%\logs\
echo.

:end
pause
