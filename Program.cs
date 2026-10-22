using StackExchange.Redis;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Interfaces;
using ATAP.DataAccess.Repositories;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.BusinessLogic.Services;
using ATAP.BlazorUI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5433;Database=atap_db;Username=postgres;Password=mysecretpassword";

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "ATAP_";
});

var cacheSettings = new CacheSettings();
builder.Configuration.GetSection("Cache").Bind(cacheSettings);
builder.Services.AddSingleton(cacheSettings);

var sqlFactory = new SqlQueryFactory();
builder.Services.AddSingleton<ISqlQueryFactory>(sqlFactory);

builder.Services.AddScoped<ICacheRepository, RedisCacheRepository>();

builder.Services.AddScoped<IUserRepository>(_ => 
    new PostgresUserRepository(connectionString, sqlFactory));
builder.Services.AddScoped<IAnalyticsRepository>(_ => 
    new PostgresAnalyticsRepository(connectionString, sqlFactory));
builder.Services.AddScoped<IMapRepository>(provider => 
    new PostgresMapRepository(
        connectionString, 
        sqlFactory, 
        provider.GetRequiredService<ICacheRepository>(),
        provider.GetRequiredService<CacheSettings>()));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<AppStateService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();