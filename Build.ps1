param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$solutionPath = Join-Path $PSScriptRoot "AccountingApp.sln"
$packagesPath = Join-Path $PSScriptRoot "packages"

if (-not (Test-Path $solutionPath)) {
    throw "Solution file was not found: $solutionPath"
}

$msbuild = Get-Command msbuild -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1

if (-not $msbuild) {
    $vsWhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vsWhere) {
        $installationPath = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($installationPath) {
            $candidate = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) {
                $msbuild = $candidate
            }
        }
    }
}

if (-not $msbuild) {
    throw "MSBuild was not found. Install Visual Studio 2019/2022 or Build Tools with the .NET desktop development workload."
}

Write-Host "Using MSBuild: $msbuild"
Write-Host "Restoring NuGet packages to: $packagesPath"

& $msbuild $solutionPath /t:Restore /p:RestorePackagesConfig=true /p:RestoreRepositoryPath="$packagesPath" /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "NuGet package restore failed with exit code $LASTEXITCODE."
}

Write-Host "Building AccountingApp ($Configuration)..."

& $msbuild $solutionPath /t:Build /p:Configuration=$Configuration /p:RestoreRepositoryPath="$packagesPath" /m /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host "Build completed successfully."
