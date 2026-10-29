using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.Notifications;

public sealed class PhamViDbThongBaoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhamViChiHuySauKhiLenhKetThucThat(bool donToken)
    {
        await using var thu = new BoThuDb(donToken);
        var phamViRunner = thu.Goc.CreateAsyncScope();
        using var huy = new CancellationTokenSource();
        var viec = thu.Gui(phamViRunner.ServiceProvider, huy.Token);
        var lenh = await thu.DaVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            huy.Cancel();
            await viec.WaitAsync(TimeSpan.FromSeconds(1));
            await phamViRunner.DisposeAsync();
            Assert.True(lenh.Huy.IsCancellationRequested);
            Assert.False(lenh.DaHuy);
            Assert.False(lenh.HuyKhiDangChay);
        }
        finally
        {
            thu.ChoXong.TrySetResult();
            await lenh.DaKetThuc.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        await lenh.DaGiaiPhong.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(lenh.DaHuy);
        Assert.False(lenh.HuyKhiDangChay);
    }

    [Fact]
    public async Task DocTokenDungContextKhacRunner()
    {
        await using var thu = new BoThuDb(false) { Treo = false };
        await using var phamVi = thu.Goc.CreateAsyncScope();
        var dbRunner = phamVi.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await thu.Gui(phamVi.ServiceProvider);
        var lenh = await thu.DaVao.Task;
        Assert.NotSame(dbRunner, lenh.Db);
        Assert.True(lenh.DaHuy);
    }

    [Fact]
    public async Task HetHanRunnerVanTruyVanDuocKhiLenhDayChuaDung()
    {
        await using var thu = new BoThuDb(false);
        await using var phamVi = thu.Goc.CreateAsyncScope();
        var dbRunner = phamVi.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var viec = thu.Gui(phamVi.ServiceProvider);
        var lenh = await thu.DaVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            thu.DongHo.Tien(TimeSpan.FromSeconds(2));
            await viec.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.True(lenh.DangChay);
            Assert.Equal(2, await dbRunner.ThietBiNhanThongBao.CountAsync());
            Assert.NotSame(dbRunner, lenh.Db);
        }
        finally
        {
            thu.ChoXong.TrySetResult();
            await lenh.DaKetThuc.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await viec.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Fact]
    public async Task ChotDauXongTrongMotGiayThiDotSauVanDayDuoc()
    {
        await using var thu = new BoThuDb(false);
        await using var mot = thu.Goc.CreateAsyncScope();
        await using var hai = thu.Goc.CreateAsyncScope();
        var viecMot = thu.Gui(mot.ServiceProvider);
        await thu.DaVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var viecHai = thu.Gui(hai.ServiceProvider);
        thu.DongHo.Tien(TimeSpan.FromSeconds(1));
        thu.ChoXong.TrySetResult();
        await Task.WhenAll(viecMot, viecHai).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2, thu.CacLenh.Count);
        Assert.Equal(2, thu.Thu.Day.Tin.Count);
        Assert.Equal(2, thu.Thu.Telegram.Tin.Count);
        Assert.Equal(1, thu.DangChayToiDa);
        Assert.Equal(0, thu.Thu.Log.Dem(LogLevel.Warning));
    }

    [Fact]
    public async Task ChotTreoQuaHaiGiayThiDotSauBoKenhDayVoiQuaHan()
    {
        await using var thu = new BoThuDb(false);
        await using var mot = thu.Goc.CreateAsyncScope();
        await using var hai = thu.Goc.CreateAsyncScope();
        using var huyMot = new CancellationTokenSource();
        var viecMot = thu.Gui(mot.ServiceProvider, huyMot.Token);
        var lenh = await thu.DaVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        huyMot.Cancel();
        await viecMot.WaitAsync(TimeSpan.FromSeconds(1));
        var viecHai = thu.Gui(hai.ServiceProvider);
        thu.DongHo.Tien(TimeSpan.FromSeconds(2));
        await viecHai.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(thu.CacLenh);
        var canhBao = Assert.Single(thu.Thu.Log.CacDong, dong => dong.Muc == LogLevel.Warning);
        Assert.Equal("doc_token", canhBao.Truong.GetValueOrDefault("GiaiDoan"));
        Assert.Equal("qua_han", canhBao.Truong.GetValueOrDefault("LoaiLoi"));
        Assert.Empty(thu.Thu.Day.Tin);
        Assert.Equal(2, thu.Thu.Telegram.Tin.Count);
        Assert.Equal(1, thu.DangChayToiDa);
        thu.ChoXong.TrySetResult();
        await lenh.DaKetThuc.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await lenh.DaGiaiPhong.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, thu.DangChayToiDa);
    }

    [Fact]
    public async Task BaDotDongThoiKhongBaoGioChayHaiThaoTacDbCungLuc()
    {
        await using var thu = new BoThuDb(false);
        await using var mot = thu.Goc.CreateAsyncScope();
        await using var hai = thu.Goc.CreateAsyncScope();
        await using var ba = thu.Goc.CreateAsyncScope();
        var viecMot = thu.Gui(mot.ServiceProvider);
        await thu.DaVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var viecHai = thu.Gui(hai.ServiceProvider);
        var viecBa = thu.Gui(ba.ServiceProvider);
        thu.DongHo.Tien(TimeSpan.FromSeconds(1));
        thu.ChoXong.TrySetResult();
        await Task.WhenAll(viecMot, viecHai, viecBa).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(3, thu.CacLenh.Count);
        Assert.Equal(1, thu.DangChayToiDa);
        Assert.Equal(3, thu.Thu.Day.Tin.Count);
        Assert.Equal(0, thu.Thu.Log.Dem(LogLevel.Warning));
    }

    [Fact]
    public async Task LoiDbDenMuonKhongRoNoiDungVaVanGiaiPhongPhamVi()
    {
        await using var thu = new BoThuDb(false) { LoiMuon = true };
        await using var phamVi = thu.Goc.CreateAsyncScope();
        using var huy = new CancellationTokenSource();
        var viec = thu.Gui(phamVi.ServiceProvider, huy.Token);
        var lenh = await thu.DaVao.Task.WaitAsync(TimeSpan.FromSeconds(2));
        huy.Cancel();
        await viec.WaitAsync(TimeSpan.FromSeconds(1));
        thu.ChoXong.TrySetResult();
        await lenh.DaGiaiPhong.Task.WaitAsync(TimeSpan.FromMilliseconds(500));
        Assert.All(thu.Thu.Log.CacDong, dong =>
        {
            Assert.Null(dong.Loi);
            Assert.DoesNotContain("synthetic-sensitive-late-db", dong.NoiDung);
        });
        Assert.False(lenh.HuyKhiDangChay);
    }

    private sealed class BoThuDb : IAsyncDisposable
    {
        internal DongHoGia DongHo { get; } = new();
        internal BoThuGuiThongBao Thu { get; } = new();
        internal ServiceProvider Goc { get; }
        internal TaskCompletionSource<KhoTheoDoi> DaVao { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ChoXong { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ConcurrentQueue<KhoTheoDoi> CacLenh { get; } = new();
        internal bool Treo { get; set; } = true;
        internal bool LoiMuon { get; set; }
        internal bool DonToken { get; }
        internal int DangChayToiDa { get; private set; }
        private readonly object khoaDem = new();
        private int soDangChay;

        internal void VaoThaoTac()
        {
            lock (khoaDem)
            {
                soDangChay++;
                if (soDangChay > DangChayToiDa) DangChayToiDa = soDangChay;
            }
        }

        internal void RaThaoTac()
        {
            lock (khoaDem) soDangChay--;
        }

        internal BoThuDb(bool donToken)
        {
            DonToken = donToken;
            Thu.DongHo = DongHo;
            if (donToken) Thu.Day.KhiGui = _ => Task.FromResult(new DayTinKetQua(1, ["synthetic-device-00001"]));
            var dichVu = Thu.TaoDichVu();
            var tuyChon = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            dichVu.RemoveAll<ApplicationDbContext>();
            dichVu.AddScoped(_ => new ApplicationDbContext(tuyChon));
            dichVu.RemoveAll<IThietBiNhanThongBaoRepository>();
            dichVu.AddScoped<IThietBiNhanThongBaoRepository>(dich => new KhoTheoDoi(this, dich.GetRequiredService<ApplicationDbContext>()));
            Goc = dichVu.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using var phamVi = Goc.CreateScope();
            var db = phamVi.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ThietBiNhanThongBao.AddRange(
                new ThietBiNhanThongBaoEntity { Token = "synthetic-device-00001", UserId = Guid.NewGuid() },
                new ThietBiNhanThongBaoEntity { Token = "synthetic-device-00002", UserId = Guid.NewGuid() });
            db.SaveChanges();
        }

        internal Task Gui(IServiceProvider dichVu, CancellationToken huy = default)
        {
            var gui = dichVu.GetRequiredService<IGuiThongBao>();
            BoThuGuiThongBao.Gom(gui);
            return gui.GuiTatCaAsync(huy);
        }
        public ValueTask DisposeAsync() { ChoXong.TrySetResult(); return Goc.DisposeAsync(); }
    }

    private sealed class KhoTheoDoi(BoThuDb thu, ApplicationDbContext db) : IThietBiNhanThongBaoRepository, IAsyncDisposable
    {
        internal ApplicationDbContext Db => db;
        internal bool DangChay { get; private set; }
        internal bool DaHuy { get; private set; }
        internal bool HuyKhiDangChay { get; private set; }
        internal CancellationToken Huy { get; private set; }
        internal TaskCompletionSource DaKetThuc { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource DaGiaiPhong { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task ChoAsync(CancellationToken huy)
        {
            Huy = huy;
            DangChay = true;
            thu.VaoThaoTac();
            using var baoVe = db.GetService<IConcurrencyDetector>().EnterCriticalSection();
            thu.CacLenh.Enqueue(this);
            thu.DaVao.TrySetResult(this);
            try
            {
                if (thu.Treo) await thu.ChoXong.Task;
                if (thu.LoiMuon) throw new InvalidOperationException("synthetic-sensitive-late-db");
            }
            finally { DangChay = false; thu.RaThaoTac(); DaKetThuc.TrySetResult(); }
        }

        public async Task<IReadOnlyList<string>> LayTokenCacMayAsync(CancellationToken cancellationToken = default)
        {
            if (!thu.DonToken) await ChoAsync(cancellationToken);
            return await new EfThietBiNhanThongBaoRepository(db).LayTokenCacMayAsync(CancellationToken.None);
        }
        public async Task XoaCacTokenAsync(IReadOnlyList<string> tokens, CancellationToken cancellationToken = default)
        {
            if (thu.DonToken) await ChoAsync(cancellationToken);
            await new EfThietBiNhanThongBaoRepository(db).XoaCacTokenAsync(tokens, CancellationToken.None);
        }
        public ValueTask DisposeAsync()
        {
            HuyKhiDangChay = DangChay;
            DaHuy = true;
            DaGiaiPhong.TrySetResult();
            return ValueTask.CompletedTask;
        }
        public Task DangKyAsync(Guid userId, string token, string nenTang, string? tokenCu = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task XoaAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> LayTokenTheoUserAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
