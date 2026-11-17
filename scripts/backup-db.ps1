# LA-179: PostgreSQL custom-format backup script (compatible with Windows PowerShell 5.1 & 7+)
[CmdletBinding()]
param(
    [string]$BackupDir = "./backups",
    [string]$Database = $env:POSTGRES_DB,
    [string]$User = $env:POSTGRES_USER,
    [string]$ContainerName = "drone_postgres"
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Database)) { $Database = "drone_delivery_db" }
if ([string]::IsNullOrWhiteSpace($User)) { $User = "postgres" }

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
}

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmss")
$backupFile = Join-Path $BackupDir "${Database}_${timestamp}.dump"

if ((Get-Command pg_dump -ErrorAction SilentlyContinue) -and -not [string]::IsNullOrWhiteSpace($env:PGHOST)) {
    $port = if ($env:PGPORT) { $env:PGPORT } else { "5432" }
    Write-Host "[backup] Dumping $Database from $($env:PGHOST):$port -> $backupFile"
    & pg_dump -h $env:PGHOST -p $port -U $User -d $Database -Fc -f $backupFile
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE"
    }
} else {
    $tmpContainerDump = "/tmp/sdd_backup_${timestamp}.dump"
    Write-Host "[backup] Dumping $Database via Docker container '$ContainerName' -> $backupFile"
    & docker exec $ContainerName pg_dump -U $User -d $Database -Fc -f $tmpContainerDump
    if ($LASTEXITCODE -ne 0) {
        throw "docker exec pg_dump failed with exit code $LASTEXITCODE"
    }
    try {
        & docker cp "${ContainerName}:${tmpContainerDump}" $backupFile
        if ($LASTEXITCODE -ne 0) {
            throw "docker cp failed with exit code $LASTEXITCODE"
        }
    } finally {
        & docker exec $ContainerName rm -f $tmpContainerDump 2>$null | Out-Null
    }
}

if (-not (Test-Path $backupFile) -or (Get-Item $backupFile).Length -eq 0) {
    throw "Backup failed or produced an empty file: $backupFile"
}

Write-Host "[backup] Completed: $backupFile ($((Get-Item $backupFile).Length) bytes)"
