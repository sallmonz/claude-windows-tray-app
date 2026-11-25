# Builds installer\Output\ClaudeUsageTray-Setup.exe from the published exe.
# Run "dotnet publish src/ClaudeUsageTray -p:PublishProfile=win-x64" first.
# Installs Inno Setup with Chocolatey or winget if it is not found.
param([string]$Version = "1.2.0")
$ErrorActionPreference = "Stop"

function Find-Iscc {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($dir in @("${env:ProgramFiles(x86)}\Inno Setup 6", "$env:ProgramFiles\Inno Setup 6",
                       "$env:LOCALAPPDATA\Programs\Inno Setup 6")) {
        if (Test-Path "$dir\ISCC.exe") { return "$dir\ISCC.exe" }
    }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc) {
    Write-Host "Inno Setup not found. Installing..."
    if (Get-Command choco -ErrorAction SilentlyContinue) {
        choco install innosetup -y --no-progress | Out-Host
    } else {
        winget install --id JRSoftware.InnoSetup -e --silent --accept-package-agreements --accept-source-agreements | Out-Host
    }
    $iscc = Find-Iscc
    if (-not $iscc) { throw "Inno Setup (ISCC.exe) is still not found." }
}

& $iscc (Join-Path $PSScriptRoot "ClaudeUsageTray.iss") "/DAppVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }
Write-Host "Installer: $(Join-Path $PSScriptRoot 'Output\ClaudeUsageTray-Setup.exe')"
