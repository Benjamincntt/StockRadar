# Deploy StockRadar len server Gdata (tu may Windows co SSH)
param(
    [string]$Server = "root@192.168.0.134",
    [string]$Domain = "stock.baobiantea.com",
    [ValidateSet("setup", "all", "fe", "be")]
    [string]$Action = "all"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==> Push code len GitHub" -ForegroundColor Cyan
Set-Location $root
git push origin master

if ($Action -eq "setup") {
    Write-Host "==> Cai dat lan dau tren server" -ForegroundColor Cyan
    ssh $Server @"
set -e
if [ ! -d /var/www/StockRadar/.git ]; then
  git clone https://github.com/Benjamincntt/StockRadar.git /var/www/StockRadar
fi
cd /var/www/StockRadar
git pull
DOMAIN=$Domain bash scripts/gdata-stockradar-setup.sh
"@
} else {
    Write-Host "==> Deploy ($Action) tren server" -ForegroundColor Cyan
    ssh $Server "cd /var/www/StockRadar && git pull && bash deploy.sh $Action"
}

Write-Host "==> Xong. Mo: http://$Domain/" -ForegroundColor Green
