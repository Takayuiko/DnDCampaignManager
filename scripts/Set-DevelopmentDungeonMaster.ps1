[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Email = 'hagges02@gmail.com',
    [switch] $CreateIfMissing
)

$ErrorActionPreference = 'Stop'
if ($CreateIfMissing) {
    $apiDirectory = Join-Path $PSScriptRoot '../DnDCampaignManager.Api'
    $previousEmail = $env:DevelopmentDm__Email
    $previousPassword = $env:DevelopmentDm__Password
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $password = Read-Host 'Password for a new test account (ignored if the account already exists)' -AsSecureString
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
    try {
        $env:DevelopmentDm__Email = $Email
        $env:DevelopmentDm__Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        Push-Location $apiDirectory
        try {
            dotnet build --output ./bin/DevelopmentSeed -p:UseAppHost=false
            if ($LASTEXITCODE -ne 0) { throw 'Development seed build failed.' }
            dotnet ./bin/DevelopmentSeed/DnDCampaignManager.Api.dll --seed-development-dm
            if ($LASTEXITCODE -ne 0) { throw 'Development account seeding failed.' }
        } finally { Pop-Location }
    } finally {
        $env:DevelopmentDm__Email = $previousEmail
        $env:DevelopmentDm__Password = $previousPassword
        $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
        $password.Dispose()
    }
    return
}
$composeFile = Join-Path $PSScriptRoot '../docker-compose.yml'
$sqlFile = Join-Path $PSScriptRoot 'promote-development-dm.sql'

# Use the credentials already configured inside the local Compose container.
# Pass the email as an argument and a quoted psql variable, never as SQL code.
Get-Content -LiteralPath $sqlFile -Raw | docker compose -f $composeFile exec -T postgres sh -c 'exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -v dm_email="$1"' -- $Email
if ($LASTEXITCODE -ne 0) {
    throw 'DM setup failed. Ensure Docker PostgreSQL is running and the account is registered.'
}
Write-Host 'Development DM setup complete. hagges02@gmail.com also receives admin access. Apply pending migrations before the promotion-only script. Sign out and sign in again.'
