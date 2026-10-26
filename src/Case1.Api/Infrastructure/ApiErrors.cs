using Case1.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
namespace Case1.Api.Infrastructure;
public sealed class ApiErrors(ILogger<ApiErrors> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var status = 500;
        var code = "beklenmeyen_hata";
        var message = "İşlem tamamlanamadı. İstek kimliği ile destek isteyebilirsiniz.";
        object? detail = null;
        if (exception is RuleViolation rule)
            (status, code, message, detail) = (rule.Status, rule.Code, rule.Message, rule.Detail);
        else if (exception is BadHttpRequestException bad)
            (status, code, message) = (bad.StatusCode, "gecersiz_istek", "JSON gövdesini ve alan türlerini kontrol edin.");
        else if (exception is SqlException sql && sql.Number is 1205 or 1222 or -2)
        {
            (status, code, message) = (503, "gecici_yogunluk", "İşlem yoğunluk nedeniyle tamamlanamadı. Aynı istek kimliğiyle tekrar deneyin.");
            context.Response.Headers.RetryAfter = "1";
        }
        if (status >= 500) logger.LogError(exception, "İşlem başarısız: {TraceId}", context.TraceIdentifier);
        var problem = new ProblemDetails { Status = status, Title = message, Instance = context.Request.Path };
        problem.Extensions["kod"] = code;
        problem.Extensions["istekKimligi"] = context.TraceIdentifier;
        if (detail is not null) problem.Extensions["ayrinti"] = detail;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
