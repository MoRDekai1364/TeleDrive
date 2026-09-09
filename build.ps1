param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$tempLogDir = Join-Path $env:TEMP "TeleDrive_Build"
New-Item -ItemType Directory -Force -Path $tempLogDir | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logPath = Join-Path $tempLogDir "build_$timestamp.log"

function Write-Log {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $Message
    Write-Host $line
    Add-Content -Path $logPath -Value $line
}

function Write-Progress-Step {
    param([int]$Percent, [string]$Status)
    $filled = [math]::Floor($Percent / 5)
    $bar = ("#" * $filled).PadRight(20, ".")
    Write-Host ("[{0}] {1}% - {2}" -f $bar, $Percent, $Status)
}

Write-Log "TeleDrive build starting. Configuration=$Configuration"
Write-Log "Log file: $logPath"

$sourceLogDir = Join-Path $root "logs"
$exitCode = 0

function Copy-LogToRepo {
    New-Item -ItemType Directory -Force -Path $sourceLogDir | Out-Null
    Copy-Item -Path $logPath -Destination $sourceLogDir -Force
    Write-Host "Log copied to: $sourceLogDir"
}

try {
    Write-Progress-Step 5 "Checking dotnet SDK"
    $dotnetVersion = dotnet --version
    Write-Log "dotnet SDK version: $dotnetVersion"

    Write-Progress-Step 15 "Restoring NuGet packages"
    $restoreOutput = dotnet restore "$root\TeleDrive.sln" 2>&1
    $restoreOutput | ForEach-Object { Add-Content -Path $logPath -Value $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Log "RESTORE FAILED. See log for details."
        $restoreOutput | Select-Object -Last 30 | ForEach-Object { Write-Host $_ }
        $exitCode = 1
        return
    }
    Write-Log "Restore succeeded."

    Write-Progress-Step 40 "Building TeleDrive.Core"
    $coreOutput = dotnet build "$root\TeleDrive.Core\TeleDrive.Core.csproj" -c $Configuration --no-restore 2>&1
    $coreOutput | ForEach-Object { Add-Content -Path $logPath -Value $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Log "TeleDrive.Core BUILD FAILED."
        $coreOutput | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }
        $exitCode = 1
        return
    }
    Write-Log "TeleDrive.Core build succeeded."

    Write-Progress-Step 70 "Building TeleDrive.WPF"
    $wpfOutput = dotnet build "$root\TeleDrive.WPF\TeleDrive.WPF.csproj" -c $Configuration --no-restore 2>&1
    $wpfOutput | ForEach-Object { Add-Content -Path $logPath -Value $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Log "TeleDrive.WPF BUILD FAILED."
        $wpfOutput | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }
        $exitCode = 1
        return
    }
    Write-Log "TeleDrive.WPF build succeeded."

    Write-Progress-Step 100 "Build complete"
    Write-Log "BUILD SUCCEEDED."
}
catch {
    Write-Log "UNEXPECTED ERROR: $($_.Exception.Message)"
    Write-Log $_.ScriptStackTrace
    $exitCode = 1
}
finally {
    Copy-LogToRepo
}

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "DONE." -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "FAILED. See errors above and in the log." -ForegroundColor Red
}

Read-Host "Press Enter to close"
exit $exitCode
