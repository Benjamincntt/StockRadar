using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockRadar.Api.Controllers;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;

namespace StockRadar.Tests.Security;

/// <summary>
/// Bảo mật API (spec 021): đăng ký đóng mặc định trả 403; sự kiện quyền chỉ admin ghi được.
/// Gọi thẳng controller với dịch vụ giả — không dựng HTTP, không thêm gói.
/// </summary>
public sealed class ApiBaoMatTests
{
    private static readonly Guid NguoiDungThuongId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void AuthOptions_MacDinh_KhongChoDangKy()
        => Assert.False(new AuthOptions().ChoPhepDangKy);

    [Fact]
    public async Task DangKyDong_Tra403_KhongGoiDichVu()
    {
        var auth = new AuthGia();
        var controller = new UsersController(auth, Options.Create(new AuthOptions { ChoPhepDangKy = false }));

        var ketQua = await controller.Create(
            new RegisterRequest("test-baomat-a@example.test", "khong-in-ra", "TEST bao mat"),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(ketQua.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.False(auth.DaGoiDangKy);
    }

    [Fact]
    public async Task DangKyMo_GoiDichVuVaTra201()
    {
        var auth = new AuthGia();
        var controller = new UsersController(auth, Options.Create(new AuthOptions { ChoPhepDangKy = true }));

        var ketQua = await controller.Create(
            new RegisterRequest("test-baomat-b@example.test", "khong-in-ra", "TEST bao mat"),
            CancellationToken.None);

        Assert.IsType<CreatedResult>(ketQua.Result);
        Assert.True(auth.DaGoiDangKy);
    }

    [Fact]
    public void SuKienQuyen_NguoiDungThuong_BiTuChoi()
    {
        var suKienQuyen = new SuKienQuyenGia();
        var controller = TaoStocksController(NguoiDungThuongId, suKienQuyen);

        var ketQua = controller.ThemSuKienQuyen(
            "HPG", new ThemSuKienQuyenRequest(new DateOnly(2026, 8, 20), 0.5m, 1m));

        Assert.IsType<ForbidResult>(ketQua.Result);
        Assert.False(suKienQuyen.DaThem);
    }

    [Fact]
    public void SuKienQuyen_Admin_DuocGhi()
    {
        var suKienQuyen = new SuKienQuyenGia();
        var controller = TaoStocksController(AdminUser.Id, suKienQuyen);

        var ketQua = controller.ThemSuKienQuyen(
            "HPG", new ThemSuKienQuyenRequest(new DateOnly(2026, 8, 20), 0.5m, 1m));

        var okResult = Assert.IsType<OkObjectResult>(ketQua.Result);
        Assert.IsType<BanGhiSuKienQuyenDto>(okResult.Value);
        Assert.True(suKienQuyen.DaThem);
    }

    private static StocksController TaoStocksController(Guid userId, IDichVuSuKienQuyen suKienQuyen) =>
        new(new StockGia(), new SectorGia(), new LookupGia(), suKienQuyen, new V3Gia(), new NguoiDungGia(userId));

    private sealed class AuthGia : IAuthService
    {
        public bool DaGoiDangKy { get; private set; }

        public Task<AuthResult> RegisterAsync(
            string email,
            string password,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            DaGoiDangKy = true;
            var user = new AuthResult(Guid.NewGuid(), email, displayName, "token-gia");
            return Task.FromResult(user);
        }

        public Task<AuthResult?> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
            => Task.FromResult<AuthResult?>(null);
    }

    private sealed class SuKienQuyenGia : IDichVuSuKienQuyen
    {
        public bool DaThem { get; private set; }

        public IReadOnlyList<BanGhiSuKienQuyenDto> LayTheoMa(string ma) => [];

        public BanGhiSuKienQuyenDto Them(string ma, ThemSuKienQuyenRequest yeuCau)
        {
            DaThem = true;
            return new BanGhiSuKienQuyenDto(
                ma.ToUpperInvariant(),
                yeuCau.ExDate,
                yeuCau.Cash,
                yeuCau.Dilution,
                yeuCau.OldShares,
                yeuCau.NewShares,
                yeuCau.IssuePrice);
        }
    }

    private sealed class NguoiDungGia(Guid userId) : ICurrentUserService
    {
        public Guid UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    private sealed class StockGia : IStockService
    {
        public Task<StockDetailDto?> GetDetailAsync(string symbol, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("không dùng trong test");

        public Task<StockChartDto?> GetChartAsync(
            string symbol,
            string interval,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("không dùng trong test");
    }

    private sealed class SectorGia : ISectorCatalogService
    {
        public Task<IReadOnlyList<string>> GetCatalogAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("không dùng trong test");

        public Task<StockSectorUpdateResultDto> UpdateStockSectorAsync(
            string symbol,
            UpdateStockSectorRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("không dùng trong test");
    }

    private sealed class LookupGia : IStockLookupService
    {
        public Task<IReadOnlyList<StockSearchHitDto>> SearchAsync(
            string query,
            int limit = 10,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("không dùng trong test");
    }

    private sealed class V3Gia : IV3HieuQuaService
    {
        public Task<HieuQuaTomTatV3Dto> LayTomTatAsync(DateOnly? tu, DateOnly? den, CancellationToken ct = default)
            => throw new NotSupportedException("không dùng trong test");

        public Task<ViTheV3PagedDto> LayLichSuAsync(int page, int size, string? status, CancellationToken ct = default)
            => throw new NotSupportedException("không dùng trong test");

        public Task<IReadOnlyList<ViTheV3Dto>> LayViTheDangGiuAsync(CancellationToken ct = default)
            => throw new NotSupportedException("không dùng trong test");

        public Task<IReadOnlyList<TinHieuV3Dto>> LayTinHieuTheoSymbolAsync(string symbol, CancellationToken ct = default)
            => throw new NotSupportedException("không dùng trong test");

        public Task<IReadOnlyDictionary<string, string>> LayTrangThaiTheoMaAsync(
            IReadOnlyCollection<string> symbols,
            CancellationToken ct = default)
            => throw new NotSupportedException("không dùng trong test");
    }
}
