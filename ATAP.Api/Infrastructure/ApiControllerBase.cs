using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Infrastructure;

public enum NegotiatedFormat { Json, Csv, NotAcceptable }

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirst("sub")!.Value);

    protected IActionResult ApiProblem(int status, string slug, string title, string? detail = null,
        IReadOnlyList<FieldError>? errors = null)
    {
        var body = Problems.Create(status, slug, title, Request.Path, detail, errors);
        return new ObjectResult(body) { StatusCode = status, ContentTypes = { Problems.ContentType } };
    }

    protected IActionResult BadData(params FieldError[] errors) =>
        ApiProblem(400, "validation-error", "Некорректные данные запроса",
            "Не заполнены обязательные поля или значения неверны", errors);

    /// <summary>Согласование формата по заголовку Accept: application/json (по умолчанию) или text/csv.</summary>
    protected NegotiatedFormat NegotiateJsonOrCsv()
    {
        var accept = Request.GetTypedHeaders().Accept;
        if (accept == null || accept.Count == 0)
            return NegotiatedFormat.Json;

        foreach (var media in accept.OrderByDescending(a => a.Quality ?? 1.0))
        {
            if (media.Quality == 0) continue;
            if (media.MediaType.Equals("text/csv", StringComparison.OrdinalIgnoreCase))
                return NegotiatedFormat.Csv;
            if (media.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                || media.MediaType.Equals("application/*", StringComparison.OrdinalIgnoreCase)
                || media.MediaType.Equals("*/*", StringComparison.OrdinalIgnoreCase))
                return NegotiatedFormat.Json;
        }
        return NegotiatedFormat.NotAcceptable;
    }

    protected IActionResult NotAcceptable() =>
        ApiProblem(406, "not-acceptable", "Формат не поддерживается",
            "Доступны application/json и text/csv");
}
