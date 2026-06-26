# Chuyen sang run.ps1 (chi stack, khong chay pipeline)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root "run.ps1") -SkipPipeline @args
