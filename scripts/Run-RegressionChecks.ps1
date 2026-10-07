param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $root 'DnDCampaignManager.Api'

function Invoke-DotNet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE." }
}

# The membership suite applies migrations first. Run sequentially because the
# database suites share fixtures and OptionalAIChecks launches a real API host.
$projects = Get-ChildItem (Join-Path $root 'tests') -Filter '*Checks.csproj' -Recurse |
    Sort-Object @{ Expression = { if ($_.BaseName -eq 'CampaignMembershipChecks') { 0 } else { 1 } } }, Name
if ($projects.Count -eq 0) { throw 'No backend regression projects were found.' }

Invoke-DotNet @('build', (Join-Path $apiDirectory 'DnDCampaignManager.Api.csproj'), '-c', $Configuration, '-p:UseAppHost=false')
$apiAssembly = Join-Path $apiDirectory "bin/$Configuration/net8.0/DnDCampaignManager.Api.dll"
foreach ($project in $projects) {
    Write-Host "Running $($project.BaseName)"
    Invoke-DotNet @('build', $project.FullName, '-c', $Configuration, '-p:UseAppHost=false')
    $assembly = Join-Path $project.DirectoryName "bin/$Configuration/net8.0/$($project.BaseName).dll"
    Invoke-DotNet @($assembly, $apiDirectory, $apiAssembly)
}
