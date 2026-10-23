<#
.SYNOPSIS
    Builds the freeserf3.net Android APK with the correct Android SDK.

.DESCRIPTION
    .NET 11 Android (workload rc.2) requires SDK platform 37 (android-37.0).
    The system SDK under "C:\Program Files (x86)\Android\android-sdk" only has
    platforms 35/36 and is not writable. A writable copy with platform 37 lives
    at C:\Users\ostermann_d\android-sdk - all Android builds must point at it
    via -p:AndroidSdkDirectory.

.EXAMPLE
    .\build-android.ps1
    .\build-android.ps1 -Configuration Release
#>
param(
    [string]$Configuration = "Debug",
    [string]$AndroidSdkDir = "C:\Users\ostermann_d\android-sdk"
)

$repoRoot = $PSScriptRoot
$csproj = Join-Path $repoRoot "Freeserf3.Android\Freeserf3.Android.csproj"

if (-not (Test-Path (Join-Path $AndroidSdkDir "platforms\android-37.0\android.jar"))) {
    Write-Warning "SDK platform 37 not found at '$AndroidSdkDir'. Build will likely fail with XA5207."
}

Write-Host "Building freeserf3.net Android ($Configuration) with SDK: $AndroidSdkDir"
dotnet build $csproj -c $Configuration "-p:AndroidSdkDirectory=$AndroidSdkDir" -p:EmbedAssembliesIntoApk=true --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host "Build succeeded."
