# Shared SQL helpers for StockRadar (read appsettings.Development.json)
# Env override: SR_SQL_SERVER, SR_SQL_DATABASE, SR_SQL_USER, SR_SQL_PASSWORD

function Get-ProjectRoot {
    Split-Path $PSScriptRoot -Parent
}

function Get-DbSettings {
    $root = Get-ProjectRoot
    $settingsPath = Join-Path $root "backend\StockRadar.Api\appsettings.Development.json"

    $server = $env:SR_SQL_SERVER
    $database = $env:SR_SQL_DATABASE
    $user = $env:SR_SQL_USER
    $password = $env:SR_SQL_PASSWORD

    if (Test-Path $settingsPath) {
        $json = Get-Content $settingsPath -Raw | ConvertFrom-Json
        $cs = $json.ConnectionStrings.DefaultConnection
        if ($cs) {
            foreach ($part in $cs.Split(';')) {
                if ([string]::IsNullOrWhiteSpace($part)) { continue }
                $kv = $part.Split('=', 2)
                if ($kv.Length -ne 2) { continue }
                $key = $kv[0].Trim()
                $value = $kv[1].Trim()
                switch -Regex ($key) {
                    '^Server$' { if (-not $server) { $server = $value } }
                    '^Database$' { if (-not $database) { $database = $value } }
                    '^User Id$' { if (-not $user) { $user = $value } }
                    '^Password$' { if (-not $password) { $password = $value } }
                }
            }
        }
    }

    if (-not $server) { $server = "localhost" }
    if (-not $database) { $database = "StockRadarDb" }

    [PSCustomObject]@{
        Server = $server
        Database = $database
        User = $user
        Password = $password
        UsesSqlAuth = -not [string]::IsNullOrWhiteSpace($user)
    }
}

function Invoke-DbSql {
    param(
        [Parameter(Mandatory = $true)][string]$Query,
        [object]$DbSettings = $null
    )

    if (-not $DbSettings) { $DbSettings = Get-DbSettings }

    $sqlArgs = @("-S", $DbSettings.Server, "-b", "-C")
    if ($DbSettings.UsesSqlAuth) {
        $sqlArgs += @("-U", $DbSettings.User, "-P", $DbSettings.Password)
    }
    else {
        $sqlArgs += "-E"
    }
    $sqlArgs += @("-Q", $Query)

    & sqlcmd @sqlArgs
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed (exit $LASTEXITCODE)."
    }
}

function Get-DbBackupPath {
    $dir = Join-Path $PSScriptRoot "db"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    Join-Path $dir "StockRadarDb.bak"
}
