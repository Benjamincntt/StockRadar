$c = New-Object System.Data.SqlClient.SqlConnection 'Server=localhost;Database=StockRadarDb;User Id=hieunl;Password=123456aH;TrustServerCertificate=True'
$c.Open()
$cmd = $c.CreateCommand()
$cmd.CommandText = 'SELECT COUNT(*) n FROM KetQuaKichBan'
$r = $cmd.ExecuteReader(); $r.Read() | Out-Null; Write-Host ('total rows: ' + $r['n']); $r.Close()
$cmd.CommandText = 'SELECT TOP 10 Id, Symbol, LoaiKichBan, TrangThai, ThoiGianKichHoat, KetQuaDoLuong, LyDoThoatHet FROM KetQuaKichBan ORDER BY Id DESC'
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host ('{0} {1} loai={2} TT={3} TGKH={4} KQ={5} LyDo={6}' -f $r['Id'], $r['Symbol'], $r['LoaiKichBan'], $r['TrangThai'], $r['ThoiGianKichHoat'], $r['KetQuaDoLuong'], $r['LyDoThoatHet'])
}
$r.Close(); $c.Close()
