param(
  [string[]]$Sql = @(),
  [switch]$Down,
  [string]$RunName = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
Set-Location $repoRoot

$composeKafka = "docs/environment/docker-compose.current.yml"
$composeFlink = "features/flink-sql-research/docker-compose.flink.yml"

function Compose([string[]]$ComposeArgs) {
  $args = @("compose", "-f", $composeKafka, "-f", $composeFlink) + $ComposeArgs
  Write-Host ("docker " + ($args -join " "))
  & docker @args
  if ($LASTEXITCODE -ne 0) { throw "docker compose failed: $($ComposeArgs -join ' ')" }
}

if ($Down) {
  Compose @("down")
  exit 0
}

# Ensure connectors (download locally, not committed)
& pwsh -NoLogo -File "features/flink-sql-research/get-connectors.ps1"
if ($LASTEXITCODE -ne 0) { throw "Failed to download Flink connectors." }

Compose @("up", "-d", "--remove-orphans")

function Wait-FlinkReady([int]$TimeoutSec = 180) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
    try {
      & docker compose -f $composeKafka -f $composeFlink exec -T flink-jobmanager bash -lc "curl -sf http://localhost:8081/ > /dev/null"
      if ($LASTEXITCODE -eq 0) { return }
    } catch { }
    Start-Sleep -Seconds 2
  }
  throw "Timeout waiting for Flink JobManager REST to become ready."
}

function Wait-FlinkJob([string]$JobId, [int]$TimeoutSec = 120) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
    $json = & docker compose -f $composeKafka -f $composeFlink exec -T flink-jobmanager bash -lc "curl -sf http://localhost:8081/jobs/$JobId"
    if ($LASTEXITCODE -ne 0) {
      Start-Sleep -Seconds 2
      continue
    }

    try {
      $obj = $json | ConvertFrom-Json
      $state = $obj.state
      if ($state -in @("FINISHED","FAILED","CANCELED")) {
        return $state
      }
    } catch {
      # ignore parse errors and retry
    }

    Start-Sleep -Seconds 2
  }
  return "TIMEOUT"
}

Wait-FlinkReady 240

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$safeName = if ([string]::IsNullOrWhiteSpace($RunName)) { "run" } else { ($RunName -replace "[^a-zA-Z0-9_-]", "_") }
$reportDir = Join-Path $repoRoot ("reports/flink_sql/{0}_{1}" -f $timestamp, $safeName)
New-Item -ItemType Directory -Path $reportDir | Out-Null

$sqlList =
  if ($Sql.Count -gt 0) { $Sql }
  else { Get-ChildItem "features/flink-sql-research/sql" -Filter "*.sql" | Sort-Object Name | ForEach-Object { $_.FullName } }

# Allow comma-separated lists (PowerShell may pass them as a single string)
$sqlList = $sqlList | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }

function Run-SqlFile([string]$FilePath) {
  $relative = Resolve-Path $FilePath
  $name = [IO.Path]::GetFileNameWithoutExtension($relative.Path)

  $originalCopy = Join-Path $reportDir ([IO.Path]::GetFileName($relative.Path))
  Copy-Item $relative.Path $originalCopy

  $renderedSql = Join-Path $reportDir ("{0}.rendered.sql" -f $name)
  Copy-Item $originalCopy $renderedSql

  $log = Join-Path $reportDir ("{0}.log" -f $name)
  $err = Join-Path $reportDir ("{0}.err.log" -f $name)

  $renderedContainerPath = "/workspace/" + ($renderedSql.Substring($repoRoot.Length).TrimStart("\").Replace("\", "/"))
  $cmdLine = "docker compose -f $composeKafka -f $composeFlink exec -T flink-jobmanager bash -lc ""/opt/flink/bin/sql-client.sh -f '$renderedContainerPath'"""
  ($cmdLine + [Environment]::NewLine) | Out-File -FilePath $log -Encoding UTF8

  & docker compose -f $composeKafka -f $composeFlink exec -T flink-jobmanager bash -lc "/opt/flink/bin/sql-client.sh -f '$renderedContainerPath'" 1>> $log 2>> $err
  $exitCode = $LASTEXITCODE

  $hasSqlError = Select-String -Path $log -Pattern "\\[ERROR\\]|Could not execute SQL statement" -Quiet -ErrorAction SilentlyContinue
  if ($hasSqlError) { $exitCode = 1 }

  $jobIds =
    (Select-String -Path $log -Pattern "Job ID:\\s*([0-9a-f]+)" -AllMatches -ErrorAction SilentlyContinue)
    | ForEach-Object { $_.Matches | ForEach-Object { $_.Groups[1].Value } }
    | Select-Object -Unique

  $jobStates = @()
  foreach ($id in $jobIds) {
    $jobStates += ("{0}:{1}" -f $id, (Wait-FlinkJob $id 180))
  }

  return [pscustomobject]@{
    Name = $name
    File = [IO.Path]::GetFileName($relative.Path)
    ExitCode = $exitCode
    Log = [IO.Path]::GetFileName($log)
    Err = [IO.Path]::GetFileName($err)
    Jobs = ($jobStates -join ", ")
  }
}

$results = @()
foreach ($f in $sqlList) {
  $results += Run-SqlFile $f
}

$flinkImage = "apache/flink:1.19.1-scala_2.12-java17"
$dockerInfo = docker version --format '{{.Server.Version}}' 2>$null

$md = @()
$md += "# Flink SQL run report"
$md += ""
$md += "- Timestamp: $timestamp"
$md += "- Name: $safeName"
$md += "- Docker: $dockerInfo"
$md += "- Flink image: $flinkImage"
$md += "- Compose: $composeKafka + $composeFlink"
$md += ""
$md += "## Results"
$md += ""
$md += "| SQL | ExitCode | stdout | stderr |"
$md += "|-----|----------|--------|--------|"
foreach ($r in $results) {
  $md += "| $($r.File) | $($r.ExitCode) | $($r.Log) | $($r.Err) |"
}
$md += ""
$md += "## Notes"
$md += "- SQL files are copied into this directory for traceability."
$md += "- Job IDs / states are recorded per SQL file (see below)."
$md += ""
$md += "## Jobs"
$md += ""
foreach ($r in $results) {
  if ([string]::IsNullOrWhiteSpace($r.Jobs)) { continue }
  $md += "- $($r.File): $($r.Jobs)"
}

$md | Out-File -FilePath (Join-Path $reportDir "report.md") -Encoding UTF8

Write-Host "OK: $reportDir"
