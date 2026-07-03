$letters = 'A','B','C','D','E','F','G','H','K','L','M','N','P','R','S','T','V'
$syms = @{}
foreach ($l in $letters) {
    try {
        $url = "http://localhost:5280/api/v1/market/stock-search?q=$l&limit=50"
        $hits = Invoke-RestMethod -Uri $url
        foreach ($h in $hits) { $syms[$h.symbol] = 1 }
    } catch {}
}
Write-Host "symbols: $($syms.Count)"
$has = 0
$none = 0
$yes = [System.Collections.Generic.List[string]]::new()
$no = [System.Collections.Generic.List[string]]::new()
foreach ($s in ($syms.Keys | Sort-Object)) {
    try {
        $d = Invoke-RestMethod -Uri "http://localhost:5280/api/v1/stocks/$s" -TimeoutSec 30
        if ($null -ne $d.basePrice) {
            $has++
            if ($yes.Count -lt 8) { $yes.Add("$s($($d.basePrice.qualityScore))") }
        } else {
            $none++
            if ($no.Count -lt 12) { $no.Add($s) }
        }
    } catch {
        Write-Host "err $s"
    }
}
$pct = [math]::Round(100 * $has / [math]::Max(1, $syms.Count), 1)
Write-Host "WITH_BASE: $has  NO_BASE: $none  pct: $pct%"
Write-Host "yes: $($yes -join ', ')"
Write-Host "no: $($no -join ', ')"
