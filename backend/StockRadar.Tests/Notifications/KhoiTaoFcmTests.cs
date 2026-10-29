using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class KhoiTaoFcmTests
{
    private const string DuLieuCam = "PRIVATE https://example.invalid/key?token=secret C:/private/service-account.json";

    [Fact]
    public void MacDinhChoNamPhutTruocKhoiTaoLai()
    {
        Assert.Equal(300, new PushNotifyOptions().KhoiTaoLaiSauGiay);
    }

    [Fact]
    public async Task KhoiTaoDongBoChamKhongChanNganXepNguoiGoi()
    {
        using var choPhep = new ManualResetEventSlim();
        var daBatDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () =>
        {
            daBatDau.TrySetResult();
            choPhep.Wait(TimeSpan.FromSeconds(2));
            return (_, _) => Task.FromResult("ok");
        });
        var doThoiGian = Stopwatch.StartNew();
        var dangGui = kenh.GuiAsync(["synthetic-device"], BoThuLogFcm.TinThu);
        var thoiGianTraTask = doThoiGian.Elapsed;
        try
        {
            Assert.True(thoiGianTraTask < TimeSpan.FromMilliseconds(500), $"Caller blocked for {thoiGianTraTask.TotalMilliseconds:F0} ms.");
            await daBatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(dangGui.IsCompleted);
        }
        finally
        {
            choPhep.Set();
            await dangGui.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HuyMotNguoiKhongHuyKhoiTaoChungKhongPhatLaiTinCu(bool coNguoiDoiKhac)
    {
        using var choPhep = new ManualResetEventSlim();
        using var huy = new CancellationTokenSource();
        var daBatDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var soKhoiTao = 0;
        var cacToken = new List<string>();
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () =>
        {
            Interlocked.Increment(ref soKhoiTao);
            daBatDau.TrySetResult();
            choPhep.Wait(TimeSpan.FromSeconds(2));
            return (tin, _) => { cacToken.Add(tin.Token); return Task.FromResult("ok"); };
        });
        var tinCu = kenh.GuiAsync(["old-canceled-device"], BoThuLogFcm.TinThu, huy.Token);
        await daBatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var tinKhac = coNguoiDoiKhac ? kenh.GuiAsync(["healthy-device"], BoThuLogFcm.TinThu) : null;
        try
        {
            huy.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tinCu.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Empty(cacToken);
            Assert.Equal(1, soKhoiTao);
            Assert.True(kenh.DangHoatDong);
        }
        finally
        {
            choPhep.Set();
            if (tinKhac is not null)
                await tinKhac.WaitAsync(TimeSpan.FromSeconds(5));
        }
        tinKhac ??= kenh.GuiAsync(["healthy-device"], BoThuLogFcm.TinThu);
        Assert.Equal(1, (await tinKhac).SoDaGui);
        Assert.Equal(new[] { "healthy-device" }, cacToken);
        Assert.Equal(1, soKhoiTao);
    }

    [Theory]
    [InlineData(0, "mang", "HttpRequestException")]
    [InlineData(1, "qua_han", "TimeoutException")]
    [InlineData(2, "qua_han", "TaskCanceledException")]
    [InlineData(3, "mang", "SocketException")]
    [InlineData(4, "qua_han", "HttpRequestException")]
    [InlineData(5, "mang", "HttpRequestException")]
    public async Task LoiTamThoiDuocPhanLoaiVaThuLaiDonLuongSau300Giay(int loai, string nhom, string kieu)
    {
        var dongHo = new DongHoFcmGia();
        var thuLog = new BoThuLogFcm();
        var soKhoiTao = 0;
        var soGui = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () =>
        {
            if (Interlocked.Increment(ref soKhoiTao) == 1)
                throw loai switch
                {
                    0 => new HttpRequestException(DuLieuCam),
                    1 => new TimeoutException(DuLieuCam),
                    2 => new TaskCanceledException(DuLieuCam),
                    3 => new SocketException((int)SocketError.TimedOut),
                    4 => new HttpRequestException(DuLieuCam, null, System.Net.HttpStatusCode.RequestTimeout),
                    _ => new HttpRequestException(DuLieuCam, null, System.Net.HttpStatusCode.ServiceUnavailable)
                };
            return (_, _) => { Interlocked.Increment(ref soGui); return Task.FromResult("ok"); };
        }, dongHo);
        var loi = await Record.ExceptionAsync(() => kenh.GuiAsync(["old-device"], BoThuLogFcm.TinThu));
        KiemTraLoiDaLamSach(loi, nhom, kieu);
        Assert.True(kenh.DangHoatDong);
        dongHo.Tien(TimeSpan.FromSeconds(299));
        for (var lan = 0; lan < 3; lan++)
            KiemTraLoiDaLamSach(await Record.ExceptionAsync(() => kenh.GuiAsync([], BoThuLogFcm.TinThu)), nhom, kieu);
        Assert.Equal(1, soKhoiTao);
        Assert.Equal(0, soGui);
        dongHo.Tien(TimeSpan.FromSeconds(1));
        var ketQua = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            kenh.GuiAsync(["new-device"], BoThuLogFcm.TinThu)));
        Assert.All(ketQua, tin => Assert.Equal(1, tin.SoDaGui));
        Assert.Equal(2, soKhoiTao);
        Assert.Equal(32, soGui);
        Assert.Equal(0, thuLog.Dem(LogLevel.Warning));
        thuLog.KiemTraKhongLo(DuLieuCam, "old-device", "new-device");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task LoiCauHinhCoDinhKhongThuLaiVaChiCanhBaoMotLan(int loai)
    {
        var dongHo = new DongHoFcmGia();
        var thuLog = new BoThuLogFcm();
        var soKhoiTao = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () =>
        {
            soKhoiTao++;
            throw loai switch
            {
                0 => new JsonException(DuLieuCam),
                1 => new FormatException(DuLieuCam),
                2 => new InvalidOperationException(DuLieuCam),
                3 => new UnauthorizedAccessException(DuLieuCam),
                _ => new FileNotFoundException(DuLieuCam, DuLieuCam)
            };
        }, dongHo);
        for (var lan = 0; lan < 3; lan++)
        {
            var ketQua = await kenh.GuiAsync(["synthetic-device"], BoThuLogFcm.TinThu);
            Assert.Equal(0, ketQua.SoDaGui);
            Assert.Empty(ketQua.TokenHong);
            Assert.False(kenh.DangHoatDong);
            dongHo.Tien(TimeSpan.FromDays(1));
        }
        Assert.Equal(1, soKhoiTao);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(2, thuLog.Dem(LogLevel.Debug));
        Assert.All(thuLog.CacDong, dong => Assert.Contains(dong.ThuocTinh,
            cap => cap.Key == "LoaiLoi" && Equals(cap.Value, "xac_thuc")));
        thuLog.KiemTraKhongLo(DuLieuCam, "synthetic-device", BoThuLogFcm.CauHinhGia().Value.Firebase.CredentialPath);
    }

    [Fact]
    public async Task DisposeTrongKhoiTaoChiDonSauKhiWorkerKetThuc()
    {
        using var choPhep = new ManualResetEventSlim();
        using var huy = new CancellationTokenSource();
        var daBatDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var daDon = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var soDon = 0;
        var soGui = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () =>
        {
            daBatDau.TrySetResult();
            choPhep.Wait(TimeSpan.FromSeconds(2));
            return (_, _) => { Interlocked.Increment(ref soGui); return Task.FromResult("ok"); };
        }, donDep: () => { Interlocked.Increment(ref soDon); daDon.TrySetResult(); });
        var dangGui = kenh.GuiAsync(["canceled-device"], BoThuLogFcm.TinThu, huy.Token);
        try
        {
            await daBatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
            huy.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dangGui.WaitAsync(TimeSpan.FromSeconds(1)));
            kenh.Dispose();
            kenh.Dispose();
            Assert.Equal(0, soDon);
            Assert.Equal(0, soGui);
        }
        finally
        {
            choPhep.Set();
        }
        await daDon.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, soDon);
        Assert.Equal(0, soGui);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => kenh.GuiAsync([], BoThuLogFcm.TinThu));
    }

    [Fact]
    public async Task DisposeDoiTatCaLanGuiDangChayVaChiDonMotLan()
    {
        var choPhep = new[]
        {
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var daBatDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var soGui = 0;
        var soDon = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () => async (_, _) =>
        {
            var viTri = Interlocked.Increment(ref soGui) - 1;
            if (viTri == 1) daBatDau.TrySetResult();
            await choPhep[viTri].Task;
            return "ok";
        }, donDep: () => Interlocked.Increment(ref soDon));
        var dau = kenh.GuiAsync(["first-device"], BoThuLogFcm.TinThu);
        var sau = kenh.GuiAsync(["second-device"], BoThuLogFcm.TinThu);
        try
        {
            await daBatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
            kenh.Dispose();
            kenh.Dispose();
            Assert.Equal(0, soDon);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => kenh.GuiAsync([], BoThuLogFcm.TinThu));
            choPhep[0].TrySetResult();
            await Task.WhenAny(dau, sau).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, soDon);
        }
        finally
        {
            foreach (var cong in choPhep) cong.TrySetResult();
        }
        await Task.WhenAll(dau, sau).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, soDon);
        Assert.Equal(2, soGui);
    }

    [Fact]
    public async Task KhoangChoTuyChinhChiThuLaiKhiCoLanGuiMoi()
    {
        var cauHinh = BoThuLogFcm.CauHinhGia();
        cauHinh.Value.KhoiTaoLaiSauGiay = 42;
        var dongHo = new DongHoFcmGia();
        var soKhoiTao = 0;
        using var kenh = new KenhDayTinFcm(cauHinh, new BoThuLogFcm(), () =>
        {
            if (++soKhoiTao == 1) throw new TimeoutException(DuLieuCam);
            return (_, _) => Task.FromResult("ok");
        }, dongHo);
        await Assert.ThrowsAsync<LoiKenhDayTinFcm>(() => kenh.GuiAsync([], BoThuLogFcm.TinThu));
        dongHo.Tien(TimeSpan.FromSeconds(41));
        await Assert.ThrowsAsync<LoiKenhDayTinFcm>(() => kenh.GuiAsync([], BoThuLogFcm.TinThu));
        Assert.Equal(1, soKhoiTao);
        dongHo.Tien(TimeSpan.FromSeconds(1));
        Assert.True(kenh.DangHoatDong);
        Assert.Equal(1, soKhoiTao);
        Assert.Equal(1, (await kenh.GuiAsync(["healthy-device"], BoThuLogFcm.TinThu)).SoDaGui);
        Assert.Equal(2, soKhoiTao);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task KhoangChoSaiLaCauHinhCoDinh(int giay)
    {
        var cauHinh = BoThuLogFcm.CauHinhGia();
        cauHinh.Value.KhoiTaoLaiSauGiay = giay;
        var soKhoiTao = 0;
        var thuLog = new BoThuLogFcm();
        using var kenh = new KenhDayTinFcm(cauHinh, thuLog, () =>
        {
            soKhoiTao++;
            return (_, _) => Task.FromResult("ok");
        });
        Assert.False(kenh.DangHoatDong);
        Assert.Empty(thuLog.CacDong);
        Assert.Equal(0, (await kenh.GuiAsync([], BoThuLogFcm.TinThu)).SoDaGui);
        cauHinh.Value.KhoiTaoLaiSauGiay = 300;
        Assert.Equal(0, (await kenh.GuiAsync([], BoThuLogFcm.TinThu)).SoDaGui);
        Assert.Equal(0, soKhoiTao);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
    }

    [Fact]
    public async Task FirebaseNullKhongNemLoiVaKhongKhoiTao()
    {
        var cauHinh = BoThuLogFcm.CauHinhGia();
        cauHinh.Value.Firebase = null!;
        using var kenh = new KenhDayTinFcm(cauHinh, new BoThuLogFcm(), () =>
            throw new InvalidOperationException(DuLieuCam));
        Assert.False(kenh.DangHoatDong);
        Assert.Equal(0, (await kenh.GuiAsync([], BoThuLogFcm.TinThu)).SoDaGui);
    }

    [Fact]
    public async Task KhoiTaoLaiDangChayKhongBiNhanDoiDuQuaHanMoi()
    {
        using var choPhep = new ManualResetEventSlim();
        var daBatDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dongHo = new DongHoFcmGia();
        var soKhoiTao = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () =>
        {
            if (Interlocked.Increment(ref soKhoiTao) == 1) throw new TimeoutException(DuLieuCam);
            daBatDau.TrySetResult();
            choPhep.Wait(TimeSpan.FromSeconds(5));
            return (_, _) => Task.FromResult("ok");
        }, dongHo);
        await Assert.ThrowsAsync<LoiKenhDayTinFcm>(() => kenh.GuiAsync([], BoThuLogFcm.TinThu));
        dongHo.Tien(TimeSpan.FromSeconds(300));
        var dau = kenh.GuiAsync(["healthy-device"], BoThuLogFcm.TinThu);
        await daBatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dongHo.Tien(TimeSpan.FromDays(1));
        var cacLan = Enumerable.Range(0, 20).Select(_ => kenh.GuiAsync(["healthy-device"], BoThuLogFcm.TinThu)).Append(dau).ToArray();
        try
        {
            Assert.Equal(2, soKhoiTao);
            Assert.All(cacLan, lan => Assert.False(lan.IsCompleted));
        }
        finally
        {
            choPhep.Set();
        }
        var ketQua = await Task.WhenAll(cacLan).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(ketQua, tin => Assert.Equal(1, tin.SoDaGui));
        Assert.Equal(2, soKhoiTao);
    }

    [Fact]
    public async Task DisposeTrongKhoiTaoChanCaNguoiDoiChuaHuy()
    {
        using var choPhep = new ManualResetEventSlim();
        var daBatDau = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var soGui = 0;
        var soDon = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), new BoThuLogFcm(), () =>
        {
            daBatDau.TrySetResult();
            choPhep.Wait(TimeSpan.FromSeconds(5));
            return (_, _) => { soGui++; return Task.FromResult("ok"); };
        }, donDep: () => Interlocked.Increment(ref soDon));
        var dangGui = kenh.GuiAsync(["healthy-device"], BoThuLogFcm.TinThu);
        try
        {
            await daBatDau.Task.WaitAsync(TimeSpan.FromSeconds(5));
            kenh.Dispose();
            Assert.Equal(0, soDon);
            Assert.False(dangGui.IsCompleted);
        }
        finally
        {
            choPhep.Set();
        }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => dangGui.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, soGui);
        Assert.Equal(1, soDon);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task LoiXacThucHttpKhongDuocThuLai(int maHttp)
    {
        var dongHo = new DongHoFcmGia();
        var thuLog = new BoThuLogFcm();
        var soKhoiTao = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog, () =>
        {
            soKhoiTao++;
            throw new HttpRequestException(DuLieuCam, null, (System.Net.HttpStatusCode)maHttp);
        }, dongHo);
        Assert.Equal(0, (await kenh.GuiAsync([], BoThuLogFcm.TinThu)).SoDaGui);
        dongHo.Tien(TimeSpan.FromDays(1));
        Assert.Equal(0, (await kenh.GuiAsync([], BoThuLogFcm.TinThu)).SoDaGui);
        Assert.Equal(1, soKhoiTao);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        thuLog.KiemTraKhongLo(DuLieuCam);
    }

    [Fact]
    public async Task DonDepLoiKhongLoNoiDungVaKhongDonLanHai()
    {
        var thuLog = new BoThuLogFcm();
        var soDon = 0;
        using var kenh = new KenhDayTinFcm(BoThuLogFcm.CauHinhGia(), thuLog,
            () => (_, _) => Task.FromResult("ok"), donDep: () =>
            {
                soDon++;
                throw new InvalidOperationException(DuLieuCam);
            });
        await kenh.GuiAsync([], BoThuLogFcm.TinThu);
        kenh.Dispose();
        kenh.Dispose();
        Assert.Equal(1, soDon);
        Assert.Equal(1, thuLog.Dem(LogLevel.Warning));
        Assert.Equal(0, thuLog.Dem(LogLevel.Debug));
        thuLog.KiemTraKhongLo(DuLieuCam);
    }

    private static void KiemTraLoiDaLamSach(Exception? loi, string nhom, string kieu)
    {
        var daLamSach = Assert.IsType<LoiKenhDayTinFcm>(loi);
        Assert.Null(daLamSach.InnerException);
        Assert.Empty(daLamSach.Data);
        Assert.DoesNotContain(DuLieuCam, daLamSach.ToString());
        Assert.Equal("khoi_tao", daLamSach.GiaiDoan);
        Assert.Equal(nhom, daLamSach.LoaiLoi);
        Assert.Equal(kieu, daLamSach.KieuLoi);
        Assert.Null(daLamSach.MessagingErrorCode);
    }

    private sealed class DongHoFcmGia : TimeProvider
    {
        private long soTick = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref soTick), TimeSpan.Zero);
        internal void Tien(TimeSpan buoc) => Interlocked.Add(ref soTick, buoc.Ticks);
    }
}
