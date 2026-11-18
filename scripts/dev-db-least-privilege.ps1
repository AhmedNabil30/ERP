<#
.SYNOPSIS
    Brings the EXISTING development database (container kaff-db, volume kaff-db-data) to the two-role model,
    without touching its data.

.DESCRIPTION
    Creates the least-privilege role kaff_app if it is absent (scripts/db/dev-app-role.sql), then applies the
    application role's grants (src/Infrastructure/Persistence/Sql/005_app_role.sql) as the owner, kaff. Safe to
    run any number of times. The API applies the same grants at every start-up, so this is only needed once,
    before the first start with the new appsettings.Development.json; it never drops or recreates anything.

    A fresh volume does not need it: docker-compose.yml runs dev-app-role.sql on first start.
    See qa/slice-3/verification-2026-11-18.md F-1.
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

foreach ($script in @(
        (Join-Path $root 'scripts/db/dev-app-role.sql'),
        (Join-Path $root 'src/Infrastructure/Persistence/Sql/005_app_role.sql'))) {
    Write-Host "Applying $script"
    # Copied in rather than piped: Windows PowerShell 5.1 re-encodes a pipe to a native program.
    docker cp $script kaff-db:/tmp/kaff-least-privilege.sql
    if ($LASTEXITCODE -ne 0) { throw "docker cp failed on $script (exit $LASTEXITCODE)." }
    docker exec kaff-db psql -U kaff -d kaff -v ON_ERROR_STOP=1 -q -f /tmp/kaff-least-privilege.sql
    if ($LASTEXITCODE -ne 0) { throw "psql failed on $script (exit $LASTEXITCODE)." }
}

docker exec kaff-db psql -U kaff -d kaff -Atc "select rolname, rolsuper, has_table_privilege('kaff_app', 'postings', 'UPDATE') from pg_roles where rolname = 'kaff_app'"
