param(
  [string]$FlinkVersion = "1.19.1",
  [string]$PluginsRoot = "tools/flink/plugins"
)

$ErrorActionPreference = "Stop"

function Ensure-Dir([string]$Path) {
  if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path | Out-Null }
}

function Download-File([string]$Url, [string]$OutFile) {
  if (Test-Path $OutFile) { return }
  Write-Host "Downloading: $Url"
  Ensure-Dir (Split-Path -Parent $OutFile)
  Invoke-WebRequest -Uri $Url -OutFile $OutFile
}

Ensure-Dir $PluginsRoot

# Notes:
# - Flink loads connector jars from plugins directory: /opt/flink/plugins/<name>/*.jar
# - We avoid committing jars; they are downloaded locally.

$base = "https://repo1.maven.org/maven2/org/apache/flink"

$targets = @(
  @{
    Name = "datagen"
    Jar  = "flink-connector-datagen-$FlinkVersion.jar"
    Url  = "$base/flink-connector-datagen/$FlinkVersion/flink-connector-datagen-$FlinkVersion.jar"
  }
)

foreach ($t in $targets) {
  $dir = Join-Path $PluginsRoot $t.Name
  $out = Join-Path $dir $t.Jar
  Download-File $t.Url $out
}

# Remove empty/obsolete plugin directories (Flink fails fast on empty plugin dirs)
$expectedNames = $targets | ForEach-Object { $_.Name }
foreach ($dir in (Get-ChildItem -Path $PluginsRoot -Directory -ErrorAction SilentlyContinue)) {
  $hasJar = (Get-ChildItem -Path $dir.FullName -Filter "*.jar" -File -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0
  if (-not $hasJar -or ($expectedNames -notcontains $dir.Name)) {
    Remove-Item -Recurse -Force $dir.FullName
  }
}

Write-Host "OK: connectors are ready under $PluginsRoot"
