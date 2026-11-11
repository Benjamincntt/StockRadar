using Microsoft.EntityFrameworkCore;
using StockRadar.Domain.Entities;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<StockEntity> Stocks => Set<StockEntity>();
    public DbSet<MarketIndexEntity> MarketIndices => Set<MarketIndexEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<WatchlistEntity> Watchlists => Set<WatchlistEntity>();
    public DbSet<WatchlistItemEntity> WatchlistItems => Set<WatchlistItemEntity>();
    public DbSet<SectorDefinitionEntity> SectorDefinitions => Set<SectorDefinitionEntity>();
    public DbSet<JobRunStatusEntity> JobRunStatuses => Set<JobRunStatusEntity>();

    public DbSet<NhatKyGiaoDichEntity> NhatKyGiaoDich => Set<NhatKyGiaoDichEntity>();

    public DbSet<NhatKyTinHieuBoQuaEntity> NhatKyTinHieuBoQua => Set<NhatKyTinHieuBoQuaEntity>();

    // === Bộ kịch bản V3 (spec 012) — thay V2 ===
    public DbSet<TinHieuV3Entity> TinHieuV3 => Set<TinHieuV3Entity>();
    public DbSet<ViTheV3Entity> ViTheV3 => Set<ViTheV3Entity>();
    public DbSet<ChiSoNgayV3Entity> ChiSoNgayV3 => Set<ChiSoNgayV3Entity>();
    public DbSet<ThietBiNhanThongBaoEntity> ThietBiNhanThongBao => Set<ThietBiNhanThongBaoEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const int moneyPrecision = 18;
        const int moneyScale = 2;

        modelBuilder.Entity<StockEntity>(e =>
        {
            e.HasKey(x => x.Symbol);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Sector).HasMaxLength(64);
            e.Property(x => x.SectorLocked).HasDefaultValue(false);
            e.Property(x => x.HistoryJson).IsRequired();
            e.Property(x => x.LastChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.LastClose).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Exchange).HasMaxLength(16);
            e.Property(x => x.AvgVolume30d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.TradingStatus).HasMaxLength(128);
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<JobRunStatusEntity>(e =>
        {
            e.ToTable("JobRunStatuses");
            e.HasKey(x => x.JobId);
            e.Property(x => x.JobId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.TriggeredBy).HasMaxLength(16);
            e.Property(x => x.Summary).HasMaxLength(512);
            e.Property(x => x.Error).HasMaxLength(1024);
        });

        modelBuilder.Entity<MarketIndexEntity>(e =>
        {
            e.HasKey(x => x.Symbol);
            e.Property(x => x.Symbol).HasMaxLength(32);
            e.Property(x => x.Price).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.HistoryJson).IsRequired();
        });

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<ThietBiNhanThongBaoEntity>(cauHinh =>
        {
            cauHinh.ToTable("ThietBiNhanThongBao");
            cauHinh.HasKey(may => may.Id);
            cauHinh.Property(may => may.Token).HasMaxLength(512).IsRequired();
            cauHinh.Property(may => may.NenTang).HasMaxLength(16).IsRequired().HasDefaultValue("android");
            cauHinh.HasIndex(may => may.Token).IsUnique();
            cauHinh.HasIndex(may => may.UserId);
            cauHinh.HasOne<UserEntity>().WithMany().HasForeignKey(may => may.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WatchlistEntity>(e =>
        {
            e.ToTable("Watchlists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.MaNganh).HasMaxLength(64);
            e.HasIndex(x => x.UserId);
            // Mỗi user chỉ 1 danh sách mặc định + không trùng danh sách ngành (bỏ qua dòng MaNganh NULL).
            e.HasIndex(x => x.UserId, "IX_Watchlists_UserId_LaMacDinh").HasFilter("[LaMacDinh] = 1").IsUnique();
            e.HasIndex(x => new { x.UserId, x.MaNganh }).HasFilter("[MaNganh] IS NOT NULL").IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WatchlistItemEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.HasIndex(x => new { x.WatchlistId, x.Symbol }).IsUnique();
            e.HasOne(x => x.Watchlist)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NhatKyGiaoDichEntity>(e =>
        {
            e.ToTable("NhatKyGiaoDich");
            e.HasKey(x => x.Id);
            e.Property(x => x.MaChungKhoan).HasMaxLength(16).IsRequired();
            // SECURITY: 'DieuChinh' dài 9 ký tự; nvarchar(8) cũ gây 500 trên SQL Server thật.
            e.Property(x => x.Chieu).HasMaxLength(16).IsRequired();
            e.Property(x => x.NguonTinHieu).HasMaxLength(8);
            e.Property(x => x.MaTinHieu).HasMaxLength(64);
            e.Property(x => x.GhiChu).HasMaxLength(512);
            e.Property(x => x.LyDo).HasMaxLength(512);
            e.Property(x => x.GiaKhop).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.KhoiLuong).HasPrecision(moneyPrecision, 4);
            e.Property(x => x.Phi).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.GiaTinHieuChup).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.KLieuChinh).HasPrecision(moneyPrecision, 4);
            e.Property(x => x.VonDieuChinh).HasPrecision(moneyPrecision, 4);
            e.HasIndex(x => new { x.UserId, x.ThoiDiemKhop });
            e.HasIndex(x => new { x.UserId, x.MaChungKhoan });
            e.HasIndex(x => new { x.UserId, x.NguonTinHieu, x.MaTinHieu });
            // SECURITY: mọi truy vấn phải lọc theo UserId (xem EfNhatKyRepositories).
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NhatKyTinHieuBoQuaEntity>(e =>
        {
            e.ToTable("NhatKyTinHieuBoQua");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nguon).HasMaxLength(8).IsRequired();
            e.Property(x => x.MaTinHieu).HasMaxLength(64).IsRequired();
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.LyDo).HasMaxLength(512);
            e.HasIndex(x => new { x.UserId, x.Nguon, x.MaTinHieu }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SectorDefinitionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<TinHieuV3Entity>(e =>
        {
            e.ToTable("TinHieuV3");
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.KetQua).HasMaxLength(16);
            e.Property(x => x.GiaTinHieu).HasPrecision(moneyPrecision, moneyScale);
            // Giá trị thước xếp hạng (RS/RSI/ret20/secr20) giữ 4 số thập phân để không làm tròn thứ hạng.
            e.Property(x => x.GiaTriXepHang).HasPrecision(18, 4);
            e.Property(x => x.GiaVao).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.GiaThoat).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.PhanTramLoiNhuan).HasPrecision(7, 4);
            e.HasIndex(x => new { x.Symbol, x.Loai, x.NgayTinHieu }).IsUnique();
            e.HasIndex(x => new { x.Loai, x.NgayTinHieu });
        });

        modelBuilder.Entity<ViTheV3Entity>(e =>
        {
            e.ToTable("ViTheV3");
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.LyDoThoat).HasMaxLength(24);
            e.Property(x => x.KetQua).HasMaxLength(16);
            e.Property(x => x.GiaVao).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.GiaThoat).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.GiaTriLenh).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.KhoiLuong).HasPrecision(moneyPrecision, 4);
            e.Property(x => x.NguongChamMa).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.GiaThamChieu).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Exchange).HasMaxLength(8);
            e.Property(x => x.PhanTramLoiNhuan).HasPrecision(7, 4);
            e.Property(x => x.Mfe).HasPrecision(7, 4);
            e.Property(x => x.Mae).HasPrecision(7, 4);
            // Cờ đối chiếu nhật ký (spec 014) — mặc định false để hàng cũ đọc được qua DTO.
            e.Property(x => x.DoiChieuMua).HasDefaultValue(false);
            e.Property(x => x.DoiChieuBan).HasDefaultValue(false);
            e.HasIndex(x => x.TinHieuV3Id);
            e.HasIndex(x => new { x.TrangThai, x.Symbol });
            e.HasIndex(x => x.NgayThoat);
        });

        modelBuilder.Entity<ChiSoNgayV3Entity>(e =>
        {
            e.ToTable("ChiSoNgayV3");
            e.HasKey(x => x.Id);
            e.Property(x => x.ChiSo).HasPrecision(18, 6);
            e.Property(x => x.ChiSoMa50).HasPrecision(18, 6);
            e.Property(x => x.ChiSoDoLechMa50PhanTram).HasPrecision(7, 4);
            e.Property(x => x.Breadth50).HasPrecision(5, 4);
            // Danh sách ~200 mã UNI của phiên kế tiếp — nvarchar(max), nhịp 08:30/14:25 đọc để nạp đúng tập mã.
            e.Property(x => x.DanhSachUni).HasColumnType("nvarchar(max)");
            e.HasIndex(x => x.Ngay).IsUnique();
        });
    }
}
