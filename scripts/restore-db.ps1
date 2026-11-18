# LA-179: PostgreSQL custom-format restore script (compatible with Windows PowerShell 5.1 & 7+)
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$TargetDatabase = "drone_delivery_db_verify",
    [string]$User = $env:POSTGRES_USER,
    [string]$ContainerName = "drone_postgres",
    [switch]$AllowOverwrite
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($User)) { $User = "postgres" }
$sourceDb = if ([string]::IsNullOrWhiteSpace($env:POSTGRES_DB)) { "drone_delivery_db" } else { $env:POSTGRES_DB }

if ($TargetDatabase -cnotmatch '\A[A-Za-z_][A-Za-z0-9_]{0,62}\z') {
    throw "TargetDatabase must be an ASCII PostgreSQL identifier (letter/underscore followed by letters, digits or underscores; maximum 63 bytes)."
}

if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

if (($TargetDatabase -eq $sourceDb) -and (-not $AllowOverwrite)) {
    throw "Refusing to overwrite source database '$sourceDb'. Restore into a verification database or pass -AllowOverwrite explicitly."
}

if ((Get-Command pg_restore -ErrorAction SilentlyContinue) -and -not [string]::IsNullOrWhiteSpace($env:PGHOST)) {
    $port = if ($env:PGPORT) { $env:PGPORT } else { "5432" }
    Write-Host "[restore] Restoring '$BackupFile' into '$TargetDatabase' on $($env:PGHOST):$port"
    $exists = & psql -X -v ON_ERROR_STOP=1 -h $env:PGHOST -p $port -U $User -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$TargetDatabase'"
    if ($LASTEXITCODE -ne 0) { throw "psql check failed with exit code $LASTEXITCODE" }
    $exists = ([string]($exists -join "`n")).Trim()
    if ($exists -eq "1" -and -not $AllowOverwrite) { throw "Refusing to overwrite existing database '$TargetDatabase'. Pass -AllowOverwrite explicitly." }
    if ($exists -ne "" -and $exists -ne "1") { throw "Unexpected database lookup result; restore refused." }
    if ($exists -eq "") {
        & psql -X -v ON_ERROR_STOP=1 -h $env:PGHOST -p $port -U $User -d postgres -c "CREATE DATABASE `"$TargetDatabase`";"
        if ($LASTEXITCODE -ne 0) { throw "CREATE DATABASE failed with exit code $LASTEXITCODE" }
    }
    & pg_restore -h $env:PGHOST -p $port -U $User -d $TargetDatabase --exit-on-error --clean --if-exists --no-owner $BackupFile
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore failed with exit code $LASTEXITCODE"
    }
} else {
    $tmpContainerDump = "/tmp/sdd_restore_$([Guid]::NewGuid().ToString('N')).dump"
    Write-Host "[restore] Restoring '$BackupFile' into '$TargetDatabase' via Docker container '$ContainerName'"
    $exists = & docker exec $ContainerName psql -X -v ON_ERROR_STOP=1 -U $User -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$TargetDatabase'"
    if ($LASTEXITCODE -ne 0) { throw "docker exec psql check failed with exit code $LASTEXITCODE" }
    $exists = ([string]($exists -join "`n")).Trim()
    if ($exists -eq "1" -and -not $AllowOverwrite) { throw "Refusing to overwrite existing database '$TargetDatabase'. Pass -AllowOverwrite explicitly." }
    if ($exists -ne "" -and $exists -ne "1") { throw "Unexpected database lookup result; restore refused." }
    if ($exists -eq "") {
        & docker exec $ContainerName psql -X -v ON_ERROR_STOP=1 -U $User -d postgres -c "CREATE DATABASE `"$TargetDatabase`";"
        if ($LASTEXITCODE -ne 0) { throw "docker exec CREATE DATABASE failed with exit code $LASTEXITCODE" }
    }
    & docker cp $BackupFile "${ContainerName}:${tmpContainerDump}"
    if ($LASTEXITCODE -ne 0) { throw "docker cp failed with exit code $LASTEXITCODE" }
    try {
        & docker exec $ContainerName pg_restore -U $User -d $TargetDatabase --exit-on-error --clean --if-exists --no-owner $tmpContainerDump
        if ($LASTEXITCODE -ne 0) {
            throw "docker exec pg_restore failed with exit code $LASTEXITCODE"
        }
    } finally {
        & docker exec $ContainerName rm -f $tmpContainerDump 2>$null | Out-Null
    }
}

Write-Host "[restore] Completed restore into '$TargetDatabase'."
