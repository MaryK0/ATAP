using StackExchange.Redis;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Interfaces;
using ATAP.DataAccess.Repositories;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.BusinessLogic.Services;
using ATAP.BlazorUI.ViewModels;
using ATAP.BlazorUI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5433;Database=atap_db;Username=postgres;Password=mysecretpassword";

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false,connectTimeout=5000";
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

var storageBackend = builder.Configuration["StorageBackend"] ?? "postgres";
var mongoConn      = builder.Configuration.GetConnectionString("MongoDB")!;
var mongoDb        = builder.Configuration["MongoDB:DatabaseName"]!;

if (storageBackend == "mongodb")
{
    builder.Services.AddScoped<IUserRepository>(_ =>
        new MongoUserRepository(mongoConn, mongoDb));
    builder.Services.AddScoped<IAnalyticsRepository>(_ =>
        new MongoAnalyticsRepository(mongoConn, mongoDb));
    builder.Services.AddScoped<IMapRepository>(p =>
        new MongoMapRepository(mongoConn, mongoDb,
            p.GetRequiredService<ICacheRepository>(),
            p.GetRequiredService<CacheSettings>()));
    builder.Services.AddScoped<ISavedReportRepository>(_ =>
        new MongoSavedReportRepository(mongoConn, mongoDb));
}
else
{
    builder.Services.AddScoped<IUserRepository>(_ =>
        new PostgresUserRepository(connectionString, sqlFactory));
    builder.Services.AddScoped<IAnalyticsRepository>(_ =>
        new PostgresAnalyticsRepository(connectionString, sqlFactory));
    builder.Services.AddScoped<IMapRepository>(p =>
        new PostgresMapRepository(connectionString, sqlFactory,
            p.GetRequiredService<ICacheRepository>(),
            p.GetRequiredService<CacheSettings>()));
    builder.Services.AddScoped<ISavedReportRepository>(_ =>
        new PostgresSavedReportRepository(connectionString, sqlFactory));
}

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISavedReportService, SavedReportService>();
builder.Services.AddScoped<AppStateService>();

// ViewModels (Transient — каждый компонент получает свой экземпляр)
builder.Services.AddTransient<LoginViewModel>();
builder.Services.AddTransient<RegisterViewModel>();
builder.Services.AddTransient<AnalyticsViewModel>();
builder.Services.AddTransient<AdminViewModel>();
builder.Services.AddTransient<MapViewModel>();
builder.Services.AddTransient<SavedReportViewModel>();

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