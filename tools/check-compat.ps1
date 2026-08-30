# Compile-compatibility matrix.
#
# Builds the mod against several Bannerlord.ReferenceAssemblies versions without
# touching the regular bin/obj outputs (everything goes to %TEMP%), so a failed
# or foreign-version build can never end up staged or deployed to the game.
#
# Usage:
#   .\tools\check-compat.ps1                          # floor + release target + latest known
#   .\tools\check-compat.ps1 -Versions 1.3.5.102453   # a specific version
#
# Available versions: https://www.nuget.org/packages/Bannerlord.ReferenceAssemblies
# Exit code 1 when any version fails to compile.

param(
    [string[]]$Versions = @(
        '1.3.5.102453',  # support floor
        '1.4.6.115628',  # release target (csproj default)
        '1.4.7.117484'   # latest known game version
    )
)

$project = Join-Path $PSScriptRoot '..\Bannerlord.EquipBestItem\Bannerlord.EquipBestItem.csproj'
$workRoot = Join-Path $env:TEMP 'ebi-compat-matrix'
$failed = @()

foreach ($v in $Versions) {
    $work = Join-Path $workRoot $v
    Write-Host "=== refs $v ===" -ForegroundColor Cyan
    $output = dotnet build $project -c Debug -v:q -nologo `
        -p:BannerlordRefsVersion=$v `
        -p:BANNERLORD_GAME_DIR= `
        -p:BaseOutputPath="$work\bin\" `
        -p:BaseIntermediateOutputPath="$work\obj\" `
        -p:MSBuildProjectExtensionsPath="$work\obj\" 2>&1
    if ($LASTEXITCODE -ne 0) {
        $failed += $v
        $output | Select-String -Pattern 'error ' | Sort-Object -Unique | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    }
    else {
        Write-Host 'OK' -ForegroundColor Green
    }
}

if ($failed) {
    Write-Host "`nIncompatible with: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "`nAll versions compile." -ForegroundColor Green
