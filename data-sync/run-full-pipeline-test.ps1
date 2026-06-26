# Chuyen sang run.ps1 o thu muc goc (1 file duy nhat)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
& (Join-Path $root "run.ps1") @args
