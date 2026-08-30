param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$solutionPath = Join-Path $PSScriptRoot "AccountingApp.sln"
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
    Write-Error "MSBuild was not found. Install Visual Studio or Build Tools with the .NET desktop development workload, then run this script again."
}

& $msbuild $solutionPath /restore /t:Build /p:Configuration=$Configuration /v:minimal
