using Microsoft.AspNetCore.Diagnostics;

namespace MiniShop.Orders.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        log.LogError(ex, "Unhandled exception {TraceId}", ctx.TraceIdentifier);
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await ctx.Response.WriteAsJsonAsync(new
        {
            type = "https://minishop.dev/errors/internal",
            title = "Internal error",
            status = 500,
            traceId = ctx.TraceIdentifier,
        }, ct);
        return true;
    }
}
