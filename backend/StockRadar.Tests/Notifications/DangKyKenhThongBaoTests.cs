using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockRadar.Application;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure;
using StockRadar.Infrastructure.Notifications;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.Notifications;

public sealed class DangKyKenhThongBaoTests
{
    /// <summary>Real DI resolves scoped dispatch/repository and singleton push without starting a host or opening SQL.</summary>
    [Fact]
    public void DangKyThatDungVongDoiVaTimeoutOptions()
    {
        var cauHinh = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "unused-offline-test",
            ["TelegramNotify:TimeoutGiay"] = "7",
            ["Push:TimeoutGiay"] = "3"
        }).Build();
        var dichVu = new ServiceCollection();
        dichVu.AddLogging();
        dichVu.AddApplication(cauHinh);
        dichVu.AddInfrastructure(cauHinh);
        using var nhaCungCap = dichVu.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var mot = nhaCungCap.CreateScope();
        using var hai = nhaCungCap.CreateScope();
        var gui = mot.ServiceProvider.GetRequiredService<IGuiThongBao>();
        Assert.IsType<GuiThongBao>(gui);
        Assert.Same(gui, mot.ServiceProvider.GetRequiredService<IGuiThongBao>());
        Assert.NotSame(gui, hai.ServiceProvider.GetRequiredService<IGuiThongBao>());
        var kho = mot.ServiceProvider.GetRequiredService<IThietBiNhanThongBaoRepository>();
        Assert.IsType<EfThietBiNhanThongBaoRepository>(kho);
        Assert.NotSame(kho, hai.ServiceProvider.GetRequiredService<IThietBiNhanThongBaoRepository>());
        var day = mot.ServiceProvider.GetRequiredService<INguiTinDay>();
        Assert.IsType<KenhDayTinFcm>(day);
        Assert.Same(day, hai.ServiceProvider.GetRequiredService<INguiTinDay>());
        Assert.False(day.DangHoatDong);
        Assert.Equal(7, nhaCungCap.GetRequiredService<IOptions<TelegramNotifyOptions>>().Value.TimeoutGiay);
        Assert.Equal(3, nhaCungCap.GetRequiredService<IOptions<PushNotifyOptions>>().Value.TimeoutGiay);
    }
}
