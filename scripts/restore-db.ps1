# LA-179: PostgreSQL custom-format restore script (PowerShell)
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$TargetDatabase = "drone_delivery_db_verify",
    [string]$User = $env:POSTGRES_USER,
    [string]$ContainerName = "drone_postgres"
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($User)) { $User = "postgres" }

if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

if ((Get-Command pg_restore -ErrorAction SilentlyContinue) -and -not [string]::IsNullOrWhiteSpace($env:PGHOST)) {
    $port = if ($env:PGPORT) { $env:PGPORT } else { "5432" }
    Write-Host "[restore] Restoring '$BackupFile' into '$TargetDatabase' on $($env:PGHOST):$port"
    $exists = & psql -h $env:PGHOST -p $port -U $User -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$TargetDatabase'"
    if ($exists -ne "1") {
        & psql -h $env:PGHOST -p $port -U $User -d postgres -c "CREATE DATABASE `"$TargetDatabase`";"
    }
    & pg_restore -h $env:PGHOST -p $port -U $User -d $TargetDatabase --clean --if-exists --no-owner $BackupFile
} else {
    Write-Host "[restore] Restoring '$BackupFile' into '$TargetDatabase' via Docker container '$ContainerName'"
    $exists = docker exec $ContainerName psql -U $User -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$TargetDatabase'"
    if ($exists -ne "1") {
        docker exec $ContainerName psql -U $User -d postgres -c "CREATE DATABASE `"$TargetDatabase`";"
    }
    Get-Content -Path $BackupFile -AsByteStream -Raw | docker exec -i $ContainerName pg_restore -U $User -d $TargetDatabase --clean --if-exists --no-owner
}

Write-Host "[restore] Completed restore into '$TargetDatabase'."
