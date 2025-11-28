# MsSqlMCP Windows Service Installation Script
# Run as Administrator

param(
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "MsSqlMCP",
    
    [Parameter(Mandatory=$false)]
    [string]$DisplayName = "MsSql MCP Server",
    
    [Parameter(Mandatory=$false)]
    [string]$Description = "Model Context Protocol server for SQL Server database inspection",
    
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Services\MsSqlMCP",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Install", "Uninstall", "Reinstall", "Status")]
    [string]$Action = "Install"
)

$ErrorActionPreference = "Stop"

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator. Right-click PowerShell and select 'Run as Administrator'."
    exit 1
}

function Get-ServiceStatus {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Host "Service '$ServiceName' exists with status: $($service.Status)" -ForegroundColor Cyan
        return $true
    } else {
        Write-Host "Service '$ServiceName' does not exist." -ForegroundColor Yellow
        return $false
    }
}

function Install-MsSqlMCPService {
    Write-Host "Installing MsSqlMCP as Windows Service..." -ForegroundColor Green
    
    # Check if service already exists
    if (Get-ServiceStatus) {
        Write-Host "Service already exists. Use -Action Reinstall to reinstall." -ForegroundColor Yellow
        return
    }
    
    # Get the script directory (where the published files are)
    $scriptDir = Split-Path -Parent $MyInvocation.ScriptName
    $publishDir = Join-Path $scriptDir "bin\Release\net10.0\win-x64\publish"
    
    if (-not (Test-Path $publishDir)) {
        Write-Host "Published files not found at: $publishDir" -ForegroundColor Yellow
        Write-Host "Publishing the application..." -ForegroundColor Cyan
        
        Push-Location $scriptDir
        dotnet publish -c Release -r win-x64 --self-contained true
        Pop-Location
        
        if (-not (Test-Path $publishDir)) {
            Write-Error "Failed to publish the application."
            exit 1
        }
    }
    
    # Create installation directory
    if (-not (Test-Path $InstallPath)) {
        Write-Host "Creating installation directory: $InstallPath" -ForegroundColor Cyan
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }
    
    # Copy files to installation directory
    Write-Host "Copying files to: $InstallPath" -ForegroundColor Cyan
    Copy-Item -Path "$publishDir\*" -Destination $InstallPath -Recurse -Force
    
    # Create logs directory
    $logsPath = Join-Path $InstallPath "logs"
    if (-not (Test-Path $logsPath)) {
        New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
    }
    
    # Get the executable path
    $exePath = Join-Path $InstallPath "MsSqlMCP.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Error "Executable not found at: $exePath"
        exit 1
    }
    
    # Create the Windows Service
    Write-Host "Creating Windows Service..." -ForegroundColor Cyan
    
    $service = New-Service -Name $ServiceName `
                          -BinaryPathName "`"$exePath`" --http-only" `
                          -DisplayName $DisplayName `
                          -Description $Description `
                          -StartupType Automatic
    
    Write-Host "Service '$ServiceName' installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review configuration at: $InstallPath\appsettings.json"
    Write-Host "  2. Start the service: Start-Service -Name $ServiceName"
    Write-Host "  3. Check logs at: $InstallPath\logs\"
    Write-Host ""
    Write-Host "Service URL: http://localhost:5000/sse" -ForegroundColor Cyan
}

function Uninstall-MsSqlMCPService {
    Write-Host "Uninstalling MsSqlMCP Windows Service..." -ForegroundColor Yellow
    
    # Check if service exists
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        Write-Host "Service '$ServiceName' does not exist." -ForegroundColor Yellow
        return
    }
    
    # Stop the service if running
    if ($service.Status -eq 'Running') {
        Write-Host "Stopping service..." -ForegroundColor Cyan
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    
    # Remove the service
    Write-Host "Removing service..." -ForegroundColor Cyan
    sc.exe delete $ServiceName | Out-Null
    
    Write-Host "Service '$ServiceName' uninstalled successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: Installation files remain at: $InstallPath" -ForegroundColor Yellow
    Write-Host "To remove files: Remove-Item -Path '$InstallPath' -Recurse -Force" -ForegroundColor Yellow
}

function Reinstall-MsSqlMCPService {
    Write-Host "Reinstalling MsSqlMCP Windows Service..." -ForegroundColor Cyan
    Uninstall-MsSqlMCPService
    Start-Sleep -Seconds 2
    Install-MsSqlMCPService
}

# Execute based on action
switch ($Action) {
    "Install" { Install-MsSqlMCPService }
    "Uninstall" { Uninstall-MsSqlMCPService }
    "Reinstall" { Reinstall-MsSqlMCPService }
    "Status" { Get-ServiceStatus }
}
