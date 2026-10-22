using System.Text;
using ATAP.Api.Auth;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.BusinessLogic.Services;
using ATAP.DataAccess;
using ATAP.DataAccess.Factories;
using ATAP.DataAccess.Interfaces;
using ATAP.DataAccess.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Конфигурация ───────────────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5433;Database=atap_db;Username=postgres;Password=mysecretpassword";
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379,abortConnect=false,connectTimeout=2000";

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

// ── Хранилище и кэш (те же слои, что у Blazor и ConsoleUI) ─────────────────────────────────
var cacheSettings = new CacheSettings();
builder.Configuration.GetSection("Cache").Bind(cacheSettings);
builder.Services.AddSingleton(cacheSettings);

var sqlFactory = new SqlQueryFactory();
builder.Services.AddSingleton<ISqlQueryFactory>(sqlFactory);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "ATAP_API_";
});
// Недоступный Redis не должен ломать запросы (NFR-R2)
builder.Services.AddScoped<ICacheRepository>(sp => new ResilientCacheRepository(
    new RedisCacheRepository(sp.GetRequiredService<IDistributedCache>()),
    sp.GetRequiredService<ILogger<ResilientCacheRepository>>()));

builder.Services.AddScoped<IUserRepository>(_ => new PostgresUserRepository(connectionString, sqlFactory));
builder.Services.AddScoped<IAnalyticsRepository>(_ => new PostgresAnalyticsRepository(connectionString, sqlFactory));
builder.Services.AddScoped<IMapRepository>(sp => new PostgresMapRepository(
    connectionString, sqlFactory, sp.GetRequiredService<ICacheRepository>(), sp.GetRequiredService<CacheSettings>()));
builder.Services.AddScoped<ISavedReportRepository>(_ => new PostgresSavedReportRepository(connectionString, sqlFactory));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<ISavedReportService, SavedReportService>();

// ── Аутентификация по JWT ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<RevokedTokenStore>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5),
            RoleClaimType = "role",
            NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            // Выход из системы: отозванный токен больше не принимается
            OnTokenValidated = context =>
            {
                var jti = context.Principal?.FindFirst("jti")?.Value;
                var store = context.HttpContext.RequestServices.GetRequiredService<RevokedTokenStore>();
                if (jti is not null && store.IsRevoked(jti))
                    context.Fail("Токен отозван");
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsJsonAsync(
                    Problems.Create(401, "unauthorized", "Требуется вход в систему", context.Request.Path),
                    (System.Text.Json.JsonSerializerOptions?)null, Problems.ContentType);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    Problems.Create(403, "forbidden", "Недостаточно прав", context.Request.Path),
                    (System.Text.Json.JsonSerializerOptions?)null, Problems.ContentType);
            }
        };
    });
builder.Services.AddAuthorization();

// ── MVC: ошибки привязки данных отдаём в формате контракта (Problem Details с полем errors) ──
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new FieldError(
                string.IsNullOrEmpty(entry.Key) || entry.Key == "$" ? "body" : ToCamelCase(entry.Key),
                string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Значение неверно" : error.ErrorMessage)))
            .ToList();
        var body = Problems.Create(400, "validation-error", "Некорректные данные запроса",
            context.HttpContext.Request.Path,
            "Не заполнены обязательные поля или значения неверны", errors);
        return new ObjectResult(body) { StatusCode = 400, ContentTypes = { Problems.ContentType } };
    };
});

var app = builder.Build();

// Необработанные исключения — тоже Problem Details, без подробностей наружу
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(
        Problems.Create(500, "internal-error", "Внутренняя ошибка сервера", context.Request.Path),
        (System.Text.Json.JsonSerializerOptions?)null, Problems.ContentType);
}));

// ── Swagger UI на согласованной спецификации (web_lr2/openapi.yaml, источник истины) ───────
var openApiDirectory = Path.Combine(AppContext.BaseDirectory, "openapi");
if (Directory.Exists(openApiDirectory))
{
    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".yaml"] = "application/yaml";
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(openApiDirectory),
        RequestPath = "/openapi",
        ContentTypeProvider = contentTypes
    });
}
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "ATAP API";
    options.SwaggerEndpoint("/openapi/openapi.yaml", "ATAP API");
    options.EnableTryItOutByDefault();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

static string ToCamelCase(string path) =>
    string.Join('.', path.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));

public partial class Program;
