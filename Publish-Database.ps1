<#
.SYNOPSIS
    Deploys the Ghurify database in the fixed release order:
    DbUp pre -> dacpac publish -> DbUp data [-> DbUp demo].

.DESCRIPTION
    Bypasses Visual Studio's bundled SSDT extension (which is incompatible with SDK-style
    .sqlproj files) by using 'dotnet build' + 'sqlpackage' directly.

    The publish profile (Ghurify.Database.publish.xml) supplies every deployment option
    (BlockOnPossibleDataLoss, DropObjectsNotInSource, ...). The TARGET comes from, in order:
        1. -ConnectionString
        2. the GHURIFY_DB environment variable
        3. the gitignored .env file at the repository root (Database__ConnectionString)
        4. the API's user-secrets ("Database:ConnectionString")
        5. the profile itself (the docker-compose container)
    so no real credential ever has to be written into a file in the repository.

.PARAMETER Configuration
    MSBuild configuration (Debug or Release). Defaults to Release.

.PARAMETER ConnectionString
    Target database. Must name the database (Database= / Initial Catalog=).

.PARAMETER DryRun
    Generates the deployment script but changes nothing: no DbUp, no publish.

.PARAMETER SchemaOnly
    Publishes the dacpac only, skipping both DbUp stages.

.PARAMETER Demo
    After the data scripts, also loads the sample hosts and trips (Scripts/Demo).
    For showcases and local development only; never for production.

.EXAMPLE
    # Full deploy to whatever user-secrets points at, plus the demo trips
    .\Publish-Database.ps1 -Demo

.EXAMPLE
    # Preview the generated SQL without touching the database
    .\Publish-Database.ps1 -DryRun
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$ConnectionString,

    [switch]$DryRun,

    [switch]$SchemaOnly,

    [switch]$Demo
)

$ErrorActionPreference = "Stop"

$RepoRoot       = $PSScriptRoot
$SqlProj        = "$RepoRoot\src\Database\Ghurify.Database\Ghurify.Database.sqlproj"
$PublishProfile = "$RepoRoot\src\Database\Ghurify.Database\Ghurify.Database.publish.xml"
$Dacpac         = "$RepoRoot\src\Database\Ghurify.Database\bin\$Configuration\Ghurify.Database.dacpac"
$OutputScript   = "$RepoRoot\src\Database\Ghurify.Database\bin\$Configuration\Ghurify.Database.sql"
$DbUpProject    = "$RepoRoot\src\Ghurify.DatabaseUpdate"

# Shared with the API and the DbUp console (see Ghurify.Api.csproj).
$UserSecretsId  = "170067e9-9eba-4275-bfc6-b0290584fd37"

# -- 0. Resolve the target, without ever printing it ---------------------------
$Source = "-ConnectionString"
if (-not $ConnectionString -and $env:GHURIFY_DB) {
    $ConnectionString = $env:GHURIFY_DB
    $Source = "GHURIFY_DB"
}
if (-not $ConnectionString) {
    # The gitignored .env at the repository root (Database__ConnectionString=...).
    $EnvFile = Join-Path $RepoRoot ".env"
    if (Test-Path $EnvFile) {
        $Line = Get-Content $EnvFile | Where-Object { $_ -match '^\s*Database(__|:)ConnectionString\s*=' } | Select-Object -Last 1
        if ($Line) {
            $Value = ($Line -split '=', 2)[1].Trim().Trim('"').Trim("'")
            if ($Value) {
                $ConnectionString = $Value
                $Source = ".env"
            }
        }
    }
}
if (-not $ConnectionString) {
    $SecretsFile = Join-Path $env:APPDATA "Microsoft\UserSecrets\$UserSecretsId\secrets.json"
    if (Test-Path $SecretsFile) {
        $Secrets = Get-Content $SecretsFile -Raw | ConvertFrom-Json
        $FromSecrets = $Secrets.'Database:ConnectionString'
        if ($FromSecrets) {
            $ConnectionString = $FromSecrets
            $Source = "user-secrets"
        }
    }
}
if (-not $ConnectionString) {
    $Source = "publish profile (docker-compose container)"
}

# Only the server and database are shown; the password never reaches the console.
if ($ConnectionString) {
    $Builder = New-Object System.Data.Common.DbConnectionStringBuilder
    # The setter, not "$Builder.ConnectionString = ...": PowerShell treats the builder as a
    # dictionary and would add a key called ConnectionString instead of parsing the string.
    $Builder.set_ConnectionString($ConnectionString)
    $Server = @('Server', 'Data Source') | Where-Object { $Builder.ContainsKey($_) } | ForEach-Object { $Builder[$_] } | Select-Object -First 1
    $Database = @('Database', 'Initial Catalog') | Where-Object { $Builder.ContainsKey($_) } | ForEach-Object { $Builder[$_] } | Select-Object -First 1
    if (-not $Database) { throw "The connection string from $Source does not name a database." }
    Write-Host "Target: $Server / $Database (from $Source)" -ForegroundColor Cyan
} else {
    Write-Host "Target: $Source" -ForegroundColor Cyan
}

$TargetArgs = @()
if ($ConnectionString) { $TargetArgs = @("/TargetConnectionString:$ConnectionString") }

function Invoke-DbUp([string]$Stage) {
    $Previous = $env:GHURIFY_DB
    try {
        if ($ConnectionString) { $env:GHURIFY_DB = $ConnectionString }
        if ($Stage) {
            dotnet run --project $DbUpProject -c $Configuration -- $Stage
        } else {
            dotnet run --project $DbUpProject -c $Configuration
        }
        if ($LASTEXITCODE -ne 0) { throw "DbUp '$Stage' stage failed (exit $LASTEXITCODE)." }
    } finally {
        $env:GHURIFY_DB = $Previous
    }
}

# -- 1. Build -----------------------------------------------------------------
Write-Host "`nBuilding dacpac ($Configuration)..." -ForegroundColor Cyan
dotnet build $SqlProj -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }

if ($DryRun) {
    Write-Host "`nGenerating deployment script (dry run, no changes to the database)..." -ForegroundColor Yellow
    sqlpackage /Action:Script /SourceFile:$Dacpac /Profile:$PublishProfile /OutputPath:$OutputScript @TargetArgs
    if ($LASTEXITCODE -ne 0) { throw "sqlpackage Script failed (exit $LASTEXITCODE)." }
    Write-Host "`nScript written to: $OutputScript" -ForegroundColor Green
    return
}

# -- 2. DbUp pre: data changes that need the OLD schema ------------------------
if (-not $SchemaOnly) {
    Write-Host "`n[1/3] DbUp pre-schema scripts..." -ForegroundColor Cyan
    Invoke-DbUp "pre"
}

# -- 3. Publish the dacpac: the only thing that changes schema -----------------
Write-Host "`n[2/3] Publishing the dacpac..." -ForegroundColor Cyan
sqlpackage /Action:Publish /SourceFile:$Dacpac /Profile:$PublishProfile @TargetArgs
if ($LASTEXITCODE -ne 0) { throw "sqlpackage Publish failed (exit $LASTEXITCODE)." }

# -- 4. DbUp data: backfills against the NEW schema ----------------------------
if (-not $SchemaOnly) {
    Write-Host "`n[3/3] DbUp data scripts..." -ForegroundColor Cyan
    Invoke-DbUp ""

    if ($Demo) {
        Write-Host "`n[demo] Loading sample hosts and trips..." -ForegroundColor Yellow
        Invoke-DbUp "demo"
    }
}

Write-Host "`nDatabase deploy succeeded." -ForegroundColor Green
