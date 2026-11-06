# Tra mã tài khoản (Guid) dùng cho V3:TaiKhoanDanhMuc. CHỈ ĐỌC bảng Users và NhatKyGiaoDich.
# Chạy từ PowerShell:  (Get-Content specs\016-remove-v1-top\tra-ma-tai-khoan.sh -Raw) -replace "`r","" | ssh -i D:/ssh/id_rsa root@103.226.248.6 'bash -s'
# Kết quả: mã tài khoản, email, số lệnh trong Nhật ký. Chọn tài khoản có số lệnh > 0 là của anh. Không in mật khẩu.
set -e
F=/var/www/publish/stockradar-api/appsettings.Production.json
CS=$(grep -o '"DefaultConnection"[^}]*' $F | head -1)
U=$(echo "$CS" | grep -oP 'User Id=\K[^;]*'); export SQLCMDPASSWORD=$(echo "$CS" | grep -oP 'Password=\K[^;"]*')
Q="SET NOCOUNT ON; SELECT CAST(u.Id AS varchar(36)) AS MaTaiKhoan, u.Email, COUNT(n.UserId) AS SoLenhNhatKy FROM Users u LEFT JOIN NhatKyGiaoDich n ON n.UserId = u.Id GROUP BY u.Id, u.Email ORDER BY COUNT(n.UserId) DESC"
/opt/mssql-tools18/bin/sqlcmd -S localhost -C -U "$U" -d StockRadarDb -W -s '|' -Q "$Q"
