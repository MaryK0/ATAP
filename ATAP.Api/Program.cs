using ATAP.Api.Auth;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.DataAccess;
using ATAP.DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.FileProviders;
using ATAP.BusinessLogic.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Конфигурация ───────────────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5433;Database=atap_db;Username=postgres;Password=mysecretpassword";
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379,abortConnect=false,connectTimeout=2000";


// ── Хранилище и кэш (те же слои, что у Blazor и ConsoleUI) ─────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "ATAP_API_";
});
// Недоступный Redis не должен ломать запросы (NFR-R2)
builder.Services.AddScoped<ICacheRepository>(sp => new ResilientCacheRepository(
    new RedisCacheRepository(sp.GetRequiredService<IDistributedCache>()),
    sp.GetRequiredService<ILogger<ResilientCacheRepository>>()));

builder.Services.AddScoped<IPlatformRepository>(_ => new PostgresPlatformRepository(connectionString));
builder.Services.AddScoped<PlatformService>();
builder.Services.AddScoped<IExportRepository>(_ => new PostgresExportRepository(connectionString));
builder.Services.AddScoped<ExportService>();
builder.Services.AddHostedService<ExportWorker>();
builder.Services.AddSingleton(TimeProvider.System);
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtSessionStore>();
builder.Services.AddSingleton<JwtTokenService>();

// Signed JWTs with server-side session revocation for immediate logout.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                var sessions = context.HttpContext.RequestServices.GetRequiredService<JwtSessionStore>();
                if (!int.TryParse(subject, out var userId) || jti is null || !sessions.IsActive(jti, userId))
                { context.Fail("Session is inactive"); return; }
                try
                {
                    var service = context.HttpContext.RequestServices.GetRequiredService<PlatformService>();
                    var user = await service.UserAsync(userId, context.HttpContext.RequestAborted);
                    // JWT role is a snapshot; current DB permissions take effect immediately.
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("sub", subject!), new Claim("jti", jti), new Claim("name", user.Username), new Claim("role", user.Role)],
                        JwtBearerDefaults.AuthenticationScheme, "name", "role"));
                }
                catch (DomainException exception) when (exception.Error == DomainError.NotFound)
                { sessions.Revoke(jti); context.Fail("User no longer exists"); }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsJsonAsync(Problems.Create(401, "unauthorized", "Требуется вход в систему", context.Request.Path),
                    (System.Text.Json.JsonSerializerOptions?)null, Problems.ContentType);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(Problems.Create(403, "forbidden", "Недостаточно прав", context.Request.Path),
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
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception is DomainException domain ? domain.Error switch
    {
        DomainError.Invalid => 400, DomainError.NotFound => 404, DomainError.Forbidden => 403,
        DomainError.Conflict => 409, DomainError.Unavailable => 503, _ => 500
    } : exception is Npgsql.NpgsqlException ? 503 : 500;
    context.Response.StatusCode = status;
    var slug = exception is DomainException d ? d.Code : status == 503 ? "service-unavailable" : "internal-error";
    var title = exception is DomainException known ? known.Message : status == 503 ? "Хранилище временно недоступно" : "Внутренняя ошибка сервера";
    await context.Response.WriteAsJsonAsync(
        Problems.Create(status, slug, title, context.Request.Path),
        (System.Text.Json.JsonSerializerOptions?)null, Problems.ContentType);
}));

// MVC can reject a Content-Type before the action executes. Keep those errors in the contract format too.
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    var status = context.Response.StatusCode;
    await context.Response.WriteAsJsonAsync(
        Problems.Create(status, status == 415 ? "unsupported-media-type" : "http-error",
            status == 415 ? "Неподдерживаемый формат тела запроса" : "Запрос не выполнен", context.Request.Path),
        (System.Text.Json.JsonSerializerOptions?)null, Problems.ContentType);
});

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
