#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates the initial EF Core migration.

.DESCRIPTION
    Slice 0 ships the model and the guard SQL but no generated migration, because no .NET SDK was
    available in the session that wrote it. Run this once, on a machine with the .NET 10 SDK, and
    commit the result.

    The guard scripts under src/Infrastructure/Persistence/Sql are NOT part of the migration. They
    are embedded resources applied by DatabaseInitializer after the schema, on every start-up, and
    they are idempotent. Keeping them out of migration history means a database can never be running
    with today's schema and last month's triggers.

.NOTES
    Requires: dotnet-ef.  dotnet tool install --global dotnet-ef
#>

param(
    [string] $Name = 'Initial'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    Write-Host "Generating migration '$Name'..."

    dotnet ef migrations add $Name `
        --project src/Infrastructure/Kaff.Infrastructure.csproj `
        --startup-project src/Api/Kaff.Api.csproj `
        --output-dir Persistence/Migrations

    if ($LASTEXITCODE -ne 0) { throw "dotnet ef failed with exit code $LASTEXITCODE." }

    Write-Host ""
    Write-Host "Done. Review the generated migration before committing, and check that:" -ForegroundColor Green
    Write-Host "  * every money column is numeric(18,4)"
    Write-Host "  * no column is named anything like 'balance'"
    Write-Host "  * the check constraints from the EF configurations are present"
    Write-Host ""
    Write-Host "Then apply it:  dotnet ef database update --project src/Infrastructure/Kaff.Infrastructure.csproj --startup-project src/Api/Kaff.Api.csproj"
}
finally {
    Pop-Location
}
