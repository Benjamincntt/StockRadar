# Xuất nến ngày của mọi mã từ DB production ra /tmp trên server (chỉ đọc).
# Chạy từ máy dev: ssh -i D:/ssh/id_rsa root@103.226.248.6 'bash -s' < export_prod.sh
# rồi: scp root@103.226.248.6:/tmp/sr_stocks.txt.gz . ; gunzip ; đổi tên thành prod_stocks.txt ; xoá file tạm trên server.
set -e
F=/var/www/publish/stockradar-api/appsettings.Production.json
CS=$(grep -o '"DefaultConnection"[^}]*' $F | head -1)
U=$(echo "$CS" | grep -oP 'User Id=\K[^;]*'); export SQLCMDPASSWORD=$(echo "$CS" | grep -oP 'Password=\K[^;"]*')
Q="SET NOCOUNT ON; SELECT Symbol + '|' + Exchange + '|' + ISNULL(Sector,'') + '|' + CAST(IsActive AS varchar(1)) + '|' + HistoryJson AS L FROM Stocks"
/opt/mssql-tools18/bin/sqlcmd -S localhost -C -U "$U" -d StockRadarDb -y 0 -o /tmp/sr_stocks.txt -Q "$Q"
gzip -f /tmp/sr_stocks.txt
