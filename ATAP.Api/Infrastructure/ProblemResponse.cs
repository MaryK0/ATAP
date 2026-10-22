using System.Text.Json.Serialization;

namespace ATAP.Api.Infrastructure;

/// <summary>Ошибка в формате Problem Details (RFC 9457), схема <c>Problem</c> контракта.</summary>
public sealed class ProblemResponse
{
    public string Type { get; set; } = "about:blank";
    public string Title { get; set; } = "";
    public int Status { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instance { get; set; }

    /// <summary>Расширение RFC 9457: ошибки по отдельным полям.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FieldError>? Errors { get; set; }
}

public sealed record FieldError(string Field, string Message);

public static class Problems
{
    public const string BaseUri = "https://atap.example/problems/";
    public const string ContentType = "application/problem+json";

    public static ProblemResponse Create(int status, string slug, string title, string? instance = null,
        string? detail = null, IReadOnlyList<FieldError>? errors = null) => new()
    {
        Type = BaseUri + slug,
        Title = title,
        Status = status,
        Detail = detail,
        Instance = instance,
        Errors = errors
    };
}
