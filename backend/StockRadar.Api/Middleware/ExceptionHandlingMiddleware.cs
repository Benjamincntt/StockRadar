using Microsoft.Data.SqlClient;
using StockRadar.Application.Common;

namespace StockRadar.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            logger.LogWarning(ex, "Application error: {Title}", ex.Title);
            await WriteProblemAsync(context, ex.Title, ex.Message, ex.StatusCode);
        }
        catch (Exception ex) when (IsTimeout(ex))
        {
            logger.LogError(ex, "Database/request timeout on {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                "Gateway Timeout",
                "Hết thời gian chờ cơ sở dữ liệu. Thử lại sau — đây không phải danh sách rỗng.",
                StatusCodes.Status504GatewayTimeout);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(
                context,
                "Internal Server Error",
                "An unexpected error occurred.",
                StatusCodes.Status500InternalServerError);
        }
    }

    private static bool IsTimeout(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
        {
            if (e is TimeoutException)
                return true;
            if (e is SqlException sql && (sql.Number == -2 || sql.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    private static Task WriteProblemAsync(HttpContext context, string title, string detail, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            status = statusCode,
            detail,
        });
    }
}
