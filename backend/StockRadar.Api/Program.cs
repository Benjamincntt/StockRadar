using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StockRadar.Api.Hubs;
using StockRadar.Api.Middleware;
using StockRadar.Api.Realtime;
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

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Stock Flow Monitor API",
        Version = "v1",
        Description = "RESTful API cho AI Stock Flow Monitor — Market, Radar, Stocks, Alerts, Watchlist, Auth"
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://localhost:5174")
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

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapHub<MarketHub>("/hubs/market");
app.MapControllers();
app.Run();
