$ErrorActionPreference = "Stop"

$solutionDir = $PSScriptRoot
$publishDir = Join-Path $solutionDir "Publish"
$installerDir = Join-Path $solutionDir "Installer"
$projectPath = Join-Path $solutionDir "src\InventoryManagement.UI\InventoryManagement.UI.csproj"
$innoScript = Join-Path $solutionDir "setup.iss"

Write-Host "Starting JeidaIMS Installer Build Process..." -ForegroundColor Cyan

# 1. Validate required tooling
$innoPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $innoPath)) {
    $innoPath = "C:\Program Files\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $innoPath)) {
        $innoPath = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        if (-not (Test-Path $innoPath)) {
            Write-Error "Inno Setup Compiler (ISCC.exe) not found. Please install Inno Setup 6."
        }
    }
}
Write-Host "Found Inno Setup at $innoPath" -ForegroundColor Green

# 2. Clean previous publish and installer output
Write-Host "Cleaning previous build outputs..."
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
if (Test-Path $installerDir) {
    Remove-Item -Path $installerDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

# 3. Publish the application
Write-Host "Publishing the application..."
$publishArgs = @(
    "publish",
    "`"$projectPath`"",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-o", "`"$publishDir`""
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
}
Write-Host "Publish successful." -ForegroundColor Green

# 4. Verify the publish output
Write-Host "Verifying publish output..."
$exePath = Join-Path $publishDir "InventoryManagement.UI.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Application executable not found in publish directory: $exePath"
}

# 5. Verify that NO development database exists in the publish output
$dbFiles = Get-ChildItem -Path $publishDir -Recurse | Where-Object { $_.Extension -in '.db', '.sqlite', '.sqlite3' }

if ($dbFiles.Count -gt 0) {
    Write-Error "Found development database files in the publish output! Aborting to prevent packaging developer data."
}
Write-Host "Publish output verified clean of database files." -ForegroundColor Green

# 6. Compile the Inno Setup installer
Write-Host "Compiling Inno Setup installer..."
$isccArgs = @(
    "`"$innoScript`""
)
& $innoPath @isccArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installerExe = Join-Path $installerDir "JeidaIMS_Installer.exe"
if (-not (Test-Path $installerExe)) {
    Write-Error "Installer executable not found at expected path: $installerExe"
}

Write-Host "Installer successfully built at: $installerExe" -ForegroundColor Green
Write-Host "DONE" -ForegroundColor Cyan
