# Deploy StockRadar len server Gdata (tu may Windows co SSH)
param(
    [string]$Server = "root@103.226.248.6",
    [string]$SshKey = "D:\ssh\id_rsa",
    [string]$Domain = "stock.baobiantea.com",
    [ValidateSet("setup", "all", "fe", "be")]
    [string]$Action = "all"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$sshArgs = @("-i", $SshKey, "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=accept-new")

Write-Host "==> Push code len GitHub" -ForegroundColor Cyan
Set-Location $root
git push origin master

if ($Action -eq "setup") {
    Write-Host "==> Cai dat lan dau tren server ($Server)" -ForegroundColor Cyan
    $remote = @"
set -e
CONN=`$(python3 -c "import json,re; c=json.load(open('/var/www/PhuKienTuiLoc/backend/appsettings.Production.json'))['ConnectionStrings']['DefaultConnection']; print(c)")
SQL_USER=`$(echo "`$CONN" | sed -n 's/.*User Id=\([^;]*\).*/\1/p')
SQL_PASSWORD=`$(echo "`$CONN" | sed -n 's/.*Password=\([^;]*\).*/\1/p')
if [ ! -d /var/www/StockRadar/.git ]; then
  git clone https://github.com/Benjamincntt/StockRadar.git /var/www/StockRadar
fi
cd /var/www/StockRadar
git pull
DOMAIN=$Domain SQL_USER=`$SQL_USER SQL_PASSWORD=`$SQL_PASSWORD bash scripts/gdata-stockradar-setup.sh
"@
    ssh @sshArgs $Server $remote
} else {
    Write-Host "==> Deploy ($Action) tren server ($Server)" -ForegroundColor Cyan
    ssh @sshArgs $Server "cd /var/www/StockRadar && git pull && bash deploy.sh $Action"
}

Write-Host "==> Xong. Mo: http://$Domain/" -ForegroundColor Green
