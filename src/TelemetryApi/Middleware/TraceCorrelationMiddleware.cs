using System.Diagnostics;

namespace TelemetryApi.Middleware;

public sealed class TraceCorrelationMiddleware(RequestDelegate next, ILogger<TraceCorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString();

        if (traceId is not null)
        {
            context.Response.Headers["X-Correlation-Id"] = traceId;

            using (logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId }))
            {
                await next(context);
                return;
            }
        }

        await next(context);
    }
}