using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StockRadar.Api.Hubs;
using StockRadar.Api.Middleware;
using StockRadar.Api.Realtime;
using StockRadar.Api.Serialization;
using StockRadar.Application;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure;
using StockRadar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(15);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(15);
});

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IMarketRealtimePublisher, SignalRMarketRealtimePublisher>();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.PayloadSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
        };
    });
builder.Services.AddAuthorization(options =>
{
    // SECURITY: bỏ FallbackPolicy thì mọi GET dữ liệu (market, stocks, watchlists, v3) trả được khi chưa đăng nhập.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// SECURITY: chặn dò mật khẩu — 5 lần/60 giây cho mỗi IP; bỏ rate limiter thì một IP đoán được vô hạn mật khẩu.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("dang-nhap", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro-ip",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0
            }));
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Stock Flow Monitor API",
        Version = "v1",
        Description = "RESTful API cho StockRadar — Market, Stocks, Watchlist, V3, Auth"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer. Đăng nhập qua POST /api/v1/auth/tokens, dán token vào đây.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                if (uri.Scheme is not ("http" or "https"))
                    return false;

                if (uri.Host is "localhost" or "127.0.0.1")
                {
                    if (builder.Environment.IsDevelopment())
                        return true;
                    return uri.Port is 5173 or 5174;
                }

                if (builder.Environment.IsDevelopment() &&
                    uri.Host.StartsWith("192.168.", StringComparison.Ordinal))
                    return uri.Port is 5173 or 5174;

                return uri.Host is
                    "baobiantea.com" or "www.baobiantea.com" or
                    "stock.baobiantea.com" or "www.stock.baobiantea.com";
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.UseForwardedHeaders();

// SECURITY: phải sau UseForwardedHeaders thì mới đếm theo IP thật của client; đặt trước là mọi người chung IP nginx và bị khoá cùng lúc.
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Stock Flow Monitor API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "AI Stock Flow Monitor API";
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();
app.UseResponseCompression();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

// SECURITY: chỉ chỉ đường, không có dữ liệu — giữ mở để kiểm sức khoẻ và xem danh sách endpoint.
app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

app.MapGet("/api/v1", () => Results.Ok(new
{
    status = "ok",
    version = "v1",
    hub = "/hubs/market",
    endpoints = new
    {
        market = "/api/v1/market",
        stocks = "/api/v1/stocks/{symbol}",
        auth = "/api/v1/auth/tokens",
    },
})).AllowAnonymous();

// SECURITY: hub chỉ đẩy giá thị trường công khai; bắt token cho WebSocket phải đưa qua query string, nginx ghi query string vào log.
app.MapHub<MarketHub>("/hubs/market").AllowAnonymous();
app.MapControllers();
app.Run();
