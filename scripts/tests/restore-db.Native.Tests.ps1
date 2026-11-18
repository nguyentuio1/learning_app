# Exercise the real Windows native argv boundary, without contacting any database.
# Function stubs cannot detect PowerShell 5.1's removal of embedded SQL quotes.
$ErrorActionPreference = 'Stop'
$fixtureDir = Join-Path ([IO.Path]::GetTempPath()) ('la46_native_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureDir | Out-Null
$restoreScript = (Resolve-Path (Join-Path $PSScriptRoot '../restore-db.ps1')).Path
$stubSource = @'
using System;
using System.IO;
public static class RestoreNativeStub {
    public static int Main(string[] args) {
        string command = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
        File.AppendAllText(Environment.GetEnvironmentVariable("RESTORE_NATIVE_LOG"), command + "\t" + String.Join("\t", args) + Environment.NewLine);
        return 0;
    }
}
'@
try {
    Add-Type -TypeDefinition $stubSource -OutputAssembly (Join-Path $fixtureDir 'stub.exe') -OutputType ConsoleApplication
    foreach ($command in @('psql', 'createdb', 'pg_restore', 'docker')) {
        Copy-Item -LiteralPath (Join-Path $fixtureDir 'stub.exe') -Destination (Join-Path $fixtureDir "$command.exe")
    }
    $backup = Join-Path $fixtureDir 'fixture.dump'
    [IO.File]::WriteAllText($backup, 'stub backup')
    $env:PATH = $fixtureDir + ';' + $env:PATH
    $env:RESTORE_NATIVE_LOG = Join-Path $fixtureDir 'native-argv.txt'
    $env:POSTGRES_DB = 'source_db'
    $failures = 0
    foreach ($mode in @('direct', 'docker')) {
        $env:PGHOST = if ($mode -eq 'direct') { 'stub-host' } else { '' }
        foreach ($target in @('UpperName', 'select')) {
            [IO.File]::WriteAllText($env:RESTORE_NATIVE_LOG, '')
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $restoreScript -BackupFile $backup -TargetDatabase $target | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Native stub restore failed: $mode/$target" }
            $calls = @(Get-Content -LiteralPath $env:RESTORE_NATIVE_LOG)
            $createCalls = @($calls | Where-Object { ($_ -split "`t") -contains 'createdb' })
            $valid = $createCalls.Count -eq 1
            if ($valid) {
                $argv = $createCalls[0] -split "`t"
                $valid = $argv[-1] -ceq $target -and $argv -contains '--maintenance-db=postgres'
            }
            if (-not $valid -or ($calls -join "`n") -match 'CREATE DATABASE') {
                $failures++; Write-Host "FAIL native argv $mode/$target`: expected createdb with unchanged database argument and maintenance database."
            } else { Write-Host "PASS native argv $mode/$target" }
        }
    }
    if ($failures) { throw "$failures native argv regression checks failed." }
} finally {
    # Delete only the explicitly named files created in this unique fixture directory.
    foreach ($name in @('stub.exe', 'psql.exe', 'createdb.exe', 'pg_restore.exe', 'docker.exe', 'fixture.dump', 'native-argv.txt')) {
        Remove-Item -LiteralPath (Join-Path $fixtureDir $name) -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $fixtureDir
}
