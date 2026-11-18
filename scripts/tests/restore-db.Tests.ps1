# Non-destructive regression checks: the real restore script runs with command stubs only.
$ErrorActionPreference = 'Stop'
$restoreScript = Join-Path $PSScriptRoot '../restore-db.ps1'
$fixture = [IO.Path]::GetTempFileName()
$failures = 0
function global:Invoke-RestoreStub {
    param([string]$Command, [object[]]$Arguments)
    $global:restoreTestCalls.Add($Command + ' ' + ($Arguments -join ' '))
    $global:LASTEXITCODE = 0
    if (($Arguments -join ' ') -match 'SELECT 1 FROM pg_database') {
        if ($global:restoreTestScenario -eq 'lookup-error') { $global:LASTEXITCODE = 2; return }
        if ($global:restoreTestScenario -ne 'missing') { return '1' }
    }
    if ($Command -eq 'pg_restore' -or $Arguments -contains 'pg_restore') {
        if ($global:restoreTestScenario -eq 'restore-error') { $global:LASTEXITCODE = 3 }
    }
}
function global:docker { Invoke-RestoreStub 'docker' $args }
function global:psql { Invoke-RestoreStub 'psql' $args }
function global:pg_restore { Invoke-RestoreStub 'pg_restore' $args }
try {
    $env:POSTGRES_DB = 'source_db'
    foreach ($mode in @('direct', 'docker')) {
        $env:PGHOST = if ($mode -eq 'direct') { 'stub-host' } else { '' }
        foreach ($case in @('existing', 'source', 'invalid', 'long', 'missing', 'allow', 'lookup-error', 'restore-error')) {
            $global:restoreTestCalls = New-Object 'System.Collections.Generic.List[string]'
            $global:restoreTestScenario = $case
            $target = 'verify_db'
            if ($case -eq 'source') { $target = 'source_db' }
            if ($case -eq 'invalid') { $target = "verify'; DROP DATABASE source_db;--" }
            if ($case -eq 'long') { $target = 'a' * 64 }
            $options = @{ BackupFile = $fixture; TargetDatabase = $target }
            if ($case -in @('allow', 'restore-error')) { $options.AllowOverwrite = $true }
            $rejected = $false
            try { & $restoreScript @options | Out-Null } catch { $rejected = $true }
            $invocations = $global:restoreTestCalls -join "`n"
            $bad = @()
            if ($case -in @('existing', 'source', 'invalid', 'long', 'lookup-error', 'restore-error') -and -not $rejected) { $bad += 'expected refusal/failure' }
            if ($case -in @('missing', 'allow') -and $rejected) { $bad += 'expected success' }
            if ($case -in @('invalid', 'long', 'source') -and $global:restoreTestCalls.Count -ne 0) { $bad += 'commands ran before validation' }
            if ($case -in @('existing', 'lookup-error') -and $invocations -match 'CREATE DATABASE|pg_restore|docker cp') { $bad += 'unsafe commands after denial' }
            if ($case -eq 'missing' -and $invocations -notmatch 'CREATE DATABASE "verify_db";') { $bad += 'missing database was not created' }
            if ($case -in @('missing', 'allow', 'restore-error') -and $invocations -notmatch 'pg_restore .*--exit-on-error') { $bad += 'restore must stop on SQL errors' }
            if ($case -eq 'allow' -and $invocations -match 'CREATE DATABASE') { $bad += 'existing database recreated' }
            if ($bad.Count) { $failures++; Write-Host "FAIL $mode/$case`: $($bad -join ', ')" }
            else { Write-Host "PASS $mode/$case" }
        }
    }
} finally {
    Remove-Item -LiteralPath $fixture
    Remove-Item Function:/docker, Function:/psql, Function:/pg_restore, Function:/Invoke-RestoreStub
}
if ($failures) { throw "$failures restore guard regression checks failed." }

