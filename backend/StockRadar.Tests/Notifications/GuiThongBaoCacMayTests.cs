using Microsoft.EntityFrameworkCore;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.Notifications;

public sealed class GuiThongBaoCacMayTests
{
    /// <summary>Broadcasting includes one device from each of two different users.</summary>
    /// <returns>Completion of broadcast integration assertions.</returns>
    [Fact]
    public async Task HaiUserHaiMayDeuNhanTin()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ThietBiNhanThongBao.AddRange(
            new ThietBiNhanThongBaoEntity { Token = "token-gia-user-a-00001", UserId = Guid.NewGuid() },
            new ThietBiNhanThongBaoEntity { Token = "token-gia-user-b-00002", UserId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var thu = new BoThuGuiThongBao();
        var gui = thu.Tao(new EfThietBiNhanThongBaoRepository(db));
        BoThuGuiThongBao.Gom(gui);
        await gui.GuiTatCaAsync();
        var tin = Assert.Single(thu.Day.Tin);
        Assert.Equal(new[] { "token-gia-user-a-00001", "token-gia-user-b-00002" }, tin.CacToken.Order());
        Assert.Single(thu.Telegram.Tin);
    }
}
