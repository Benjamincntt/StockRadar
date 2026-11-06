set -e
F=/var/www/publish/stockradar-api/appsettings.Production.json
CS=$(grep -o '"DefaultConnection"[^}]*' $F | head -1)
U=$(echo "$CS" | grep -oP 'User Id=\K[^;]*'); export SQLCMDPASSWORD=$(echo "$CS" | grep -oP 'Password=\K[^;"]*')
Q="SET NOCOUNT ON; SELECT CONVERT(varchar(10),ForTradingDate,23),Symbol,Rank,ISNULL(Recommendation,''),ISNULL(TradeState,''),ISNULL(CAST(BuyScore AS varchar(20)),''),CONVERT(varchar(19),GeneratedAt,120),ISNULL(CAST(Price AS varchar(20)),'') FROM DailyOpportunities ORDER BY ForTradingDate,Rank"
/opt/mssql-tools18/bin/sqlcmd -S localhost -C -U "$U" -d StockRadarDb -h -1 -W -s '|' -Q "$Q"
