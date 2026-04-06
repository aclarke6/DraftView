<#
.SYNOPSIS
    Launches psql with credentials sourced from user-secrets.

.DESCRIPTION
    Reads the Postgres password from dotnet user-secrets for DraftView.Web,
    sets PGPASSWORD for the session, and forwards all arguments to psql.

.EXAMPLE
    ./pg1.ps1
    ./pg1.ps1 -c "\dt"
#>

$ErrorActionPreference = 'Stop'

# Resolve Postgres password from user-secrets
$env:PGPASSWORD = (
    dotnet user-secrets list --project DraftView.Web |
    Where-Object { $_ -match "PostgresPassword" } |
    ForEach-Object { $_ -replace "PostgresPassword = ", "" }
)

if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) {
    throw "PostgresPassword not found in user-secrets for DraftView.Web"
}

# Build argument list
$pgArgs = @("-U", "postgres", "-d", "draftview") + $args

# Execute psql
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" @pgArgs